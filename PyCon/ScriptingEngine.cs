using IronPython.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Twist.PyCon
{
    public interface IScript
    {
        string FilePath { get; }
        string Name { get; }
        string Content { get; }
    }

    public class PythonScript : IScript
    {
        public string FilePath { get; private set; }
        public string Name { get { return Path.GetFileNameWithoutExtension(FilePath); } }
        public string Content { get; private set; }

        public PythonScript(string path)
        {
            FilePath = path;
            Content = File.ReadAllText(path);
        }
    }

    public enum ScriptUnitState
    {
        Idle = 0, Ready = 10,
        Done = 20, Aborted = 30, Error = 40,
        Busy = 50,
        Running = 60, Breaking = 70, Aborting = 80,
    }

    public interface IScriptUnit
    {
        string Name { get; }

        //void Load(IScript script);
        void AppendGlobals(IDictionary<string, dynamic> globals);
        void AppendGlobal(string name, dynamic global);
        Task Run();
        Task<dynamic> Execute();
        void Reset();
        bool IsDebugEnabled { get; }
        void Abort();
        void Resume();
        void Break();
        void RequestBreak();

        void SetBreakpoint(int line);
        void ClearBreakpoint(int line);
        void ClearAllBreakpoints();

        ScriptScope ScriptScope { get; }
        ScriptEngine ScriptEngine { get; }

        ScriptUnitState State { get; }
        Exception LastError { get; }
    }

    public class ScriptUnitException : Exception
    {

    }

    public class InvalidScriptUnitStateException : ScriptUnitException
    {

    }

    public class PythonScriptUnitAbortException : ScriptUnitException
    {
        //used to force script abort
    }

    public interface IScriptUnitEvent
    {
        IScriptUnit ScriptUnit { get; }
    }

    public interface IScriptUnitProgressEvent : IScriptUnitEvent
    {
        int LineNumber { get; }
    }

    public class ScriptUnitProgressEvent : IScriptUnitProgressEvent
    {
        private int lineNumber;
        public int LineNumber
        {
            get { return lineNumber; }
        }
        private IScriptUnit unit;
        public IScriptUnit ScriptUnit
        {
            get { return unit; }
        }

        public ScriptUnitProgressEvent(IScriptUnit unit, int lineNumber)
        {
            this.unit = unit;
            this.lineNumber = lineNumber;
        }
    }


    public interface IScriptUnitStateEvent : IScriptUnitEvent
    {
        ScriptUnitState State { get; }
        Exception Error { get; }
    }

    public class ScriptUnitStateEvent : IScriptUnitStateEvent
    {
        private ScriptUnitState state;
        public ScriptUnitState State
        {
            get { return state; }
        }
        private Exception error;
        public Exception Error { get { return error; } }
        private IScriptUnit unit;
        public IScriptUnit ScriptUnit
        {
            get { return unit; }
        }

        public ScriptUnitStateEvent(IScriptUnit unit, ScriptUnitState state, Exception error = null)
        {
            this.unit = unit;
            this.state = state;
            this.error = error;
        }
    }


    public class PythonScriptUnit : ViewModelBase, IScriptUnit
    {

        private int _lineNumber;
        public int LineNumber
        {
            get { return _lineNumber; }
            set
            {
                _lineNumber = value;
                OnPropertyChanged(nameof(LineNumber));
            }
        }


        private string _status;
        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        protected string name;
        protected ScriptEngine engine;
        protected ScriptScope scope;
        protected ScriptScope builtinModule;
        protected IScript script;
        protected ScriptSource source;
        protected CompiledCode compiled;
        protected IronPython.Runtime.Exceptions.TracebackDelegate traceDelegate;

        protected List<int> breakpoints = new List<int>();

        protected AutoResetEvent resumeEvent = new AutoResetEvent(false);
        protected AutoResetEvent breakEvent = new AutoResetEvent(false);
        protected AutoResetEvent abortEvent = new AutoResetEvent(false);
        protected AutoResetEvent requestBreakEvent = new AutoResetEvent(false);

        protected IDictionary<string, dynamic> globals = new Dictionary<string, dynamic>();
        protected List<string> searchPaths = new List<string>();

        public ScriptScope ScriptScope { get { return scope; } }
        public ScriptEngine ScriptEngine { get { return engine; } }

        protected bool isDebugEnabled;
        public bool IsDebugEnabled { get { return isDebugEnabled; } }

        private volatile ScriptUnitState currentState = ScriptUnitState.Idle;
        public ScriptUnitState State { get { return currentState; } }

        private Exception lastError;
        public Exception LastError { get { return lastError; } }
        private int lastLine = -1;

        public bool IsBusy
        {
            get { return currentState >= ScriptUnitState.Busy; }
        }

        public string Name { get { return name; } }

        public PythonScriptUnit(PythonScript sourceScript, bool enableDebug = true, ScriptScope scriptScope = null)
        {
            this.name = sourceScript.Name;
            this.script = sourceScript;

            Dictionary<string, object> options = new Dictionary<string, object>();
            isDebugEnabled = enableDebug;
            options["Debug"] = enableDebug;
            engine = Python.CreateEngine(options);
            //setup trace delegate
            traceDelegate = new IronPython.Runtime.Exceptions.TracebackDelegate(TraceCallback);
            if (enableDebug)
                engine.SetTrace(traceDelegate);

            if (scriptScope == null)
                scope = engine.CreateScope();
            else
                scope = scriptScope;

            builtinModule = engine.GetBuiltinModule();
            builtinModule.SetVariable("WaitAll", new Action<Task[]>((tasks) => Task.WaitAll(tasks)));
            builtinModule.SetVariable("TaskSleep", new Func<double, Task>((secs) => TaskSleep(secs)));
            builtinModule.SetVariable("StartLambda", new Func<Func<dynamic>, Task>((action) => Task.Run(action)));
            builtinModule.SetVariable("AllowBreak", new Action(() => AllowBreak()));
            builtinModule.SetVariable("Break", new Action(() => Break()));
            builtinModule.SetVariable("WaitBreak", new Action(() => { })); //empty method so the break will happen there 

            source = engine.CreateScriptSourceFromString(script.Content, SourceCodeKind.File);

            try
            {
                compiled = source.Compile();

                currentState = ScriptUnitState.Ready;
                PublishEvent(new ScriptUnitStateEvent(this, currentState));
            }
            catch (SyntaxErrorException ex)
            {
                currentState = ScriptUnitState.Error;
                PublishEvent(new ScriptUnitStateEvent(this, currentState));
                throw ex;
            }
        }



        public Task TaskSleep(double seconds)
        {
            return Task.Delay((int)Math.Round(seconds * 1000));
        }


        protected IronPython.Runtime.Exceptions.TracebackDelegate TraceCallback(IronPython.Runtime.Exceptions.TraceBackFrame frame, string eventName, object payload)
        {
            bool abort = false;
            abort = abortEvent.WaitOne(0); //test abort signal

            int currentFrameLine = (int)frame.f_lineno;

            if (currentFrameLine != lastLine)
            {
                lastLine = currentFrameLine;
                PublishEvent(new ScriptUnitProgressEvent(this, source != null ? source.MapLine(currentFrameLine) : currentFrameLine));
            }


            if (!abort)
            {
                bool breaking = breakEvent.WaitOne(0); //test if a break was requested
                if (!breaking)
                {
                    int currentLine = source != null ? source.MapLine(currentFrameLine) : currentFrameLine;
                    lock (breakpoints)
                    {
                        breaking |= breakpoints.Contains(currentLine);
                    }
                }

                if (breaking)
                {
                    currentState = ScriptUnitState.Breaking;
                    PublishEvent(new ScriptUnitStateEvent(this, currentState));

                    //break!
                    //wait for resume event (or abort as we could want to abort while debugging)
                    int eventIndex = AutoResetEvent.WaitAny(new WaitHandle[] { resumeEvent, abortEvent });


                    if (eventIndex == 1)
                        abort = true;
                    else if (eventIndex == 0)
                    {
                        breaking = false;
                        currentState = ScriptUnitState.Running;

                        PublishEvent(new ScriptUnitStateEvent(this, currentState));
                    }
                }
            }

            if (abort)
            {
                breakEvent.Reset();
                requestBreakEvent.Reset();
                currentState = ScriptUnitState.Aborting;
                PublishEvent(new ScriptUnitStateEvent(this, currentState));
                throw new PythonScriptUnitAbortException();
            }

            return traceDelegate;
        }

        protected void PublishEvent(ScriptUnitStateEvent e)
        {
            //stateUpdates.OnNext(e);
            Status = e.State.ToString();
        }

        protected void PublishEvent(ScriptUnitProgressEvent e)
        {
            LineNumber = e.LineNumber;
        }

        public void AppendSearchPath(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!searchPaths.Contains(path))
                    searchPaths.Add(path);
            }
        }

        public void AppendGlobal(string name, dynamic global)
        {
            Dictionary<string, dynamic> g = new Dictionary<string, dynamic>();
            g.Add(name, global);
            AppendGlobals(g);
        }

        public void AppendGlobals(IDictionary<string, dynamic> globals)
        {
            foreach (var g in globals)
            {
                this.globals[g.Key] = g.Value;
            }

            foreach (var g in globals)
                builtinModule.SetVariable(g.Key, g.Value);
        }


        public void ReloadSearchPath()
        {
            var engineSearchPaths = engine.GetSearchPaths().ToList();
            if (script != null)
            {
                string scriptDirectoryName = Path.GetDirectoryName(script.FilePath);
                if (!engineSearchPaths.Contains(scriptDirectoryName))
                    engineSearchPaths.Add(scriptDirectoryName); //add local directory if not already in there
            }
            foreach (string path in searchPaths)
            {
                if (!engineSearchPaths.Contains(path))
                    engineSearchPaths.Add(path); //add search paths
            }
            engine.SetSearchPaths(engineSearchPaths);
        }


        public void Reset()
        {
            lock (this)
            {
                if (IsBusy)
                    throw new InvalidScriptUnitStateException();
                if (compiled != null && script != null)
                    currentState = ScriptUnitState.Ready;
                else
                    currentState = ScriptUnitState.Idle;
                breakEvent.Reset();
                requestBreakEvent.Reset();
                PublishEvent(new ScriptUnitStateEvent(this, currentState));
            }
        }

        protected Task runTask;

        public Task Run()
        {
            lock (this)
            {
                if (currentState != ScriptUnitState.Ready)
                    throw new InvalidScriptUnitStateException();

                return Task.Run(() =>
                {
                    try
                    {
                        lastLine = -1;
                        lastError = null;
                        currentState = ScriptUnitState.Running;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState));

                        if (isDebugEnabled)
                            engine.SetTrace(traceDelegate); //set again trace at beginning
                        foreach (var g in globals)
                            builtinModule.SetVariable(g.Key, g.Value);

                        compiled.Execute(scope);

                        currentState = ScriptUnitState.Done;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState));
                    }
                    catch (PythonScriptUnitAbortException ex)
                    {
                        //script was aborted through the abort exception
                        currentState = ScriptUnitState.Aborted;
                        lastError = null;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState));
                    }
                    catch (Exception ex)
                    {
                        currentState = ScriptUnitState.Error;
                        lastError = ex;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState, ex));
                    }
                });
            }
        }

        public Task<dynamic> Execute()
        {
            lock (this)
            {
                if (currentState != ScriptUnitState.Ready) Reset();

                if (currentState != ScriptUnitState.Ready)
                    throw new InvalidScriptUnitStateException();

                return Task.Run<dynamic>(() =>
                {
                    try
                    {
                        lastLine = -1;
                        lastError = null;
                        currentState = ScriptUnitState.Running;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState));

                        if (isDebugEnabled)
                            engine.SetTrace(traceDelegate); //set again trace at beginning
                        foreach (var g in globals)
                            builtinModule.SetVariable(g.Key, g.Value);

                        var result = compiled.Execute(scope);

                        currentState = ScriptUnitState.Done;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState));

                        return result;
                    }
                    catch (PythonScriptUnitAbortException ex)
                    {
                        //script was aborted through the abort exception
                        currentState = ScriptUnitState.Aborted;
                        lastError = null;

                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        currentState = ScriptUnitState.Error;
                        lastError = ex;
                        PublishEvent(new ScriptUnitStateEvent(this, currentState, ex));

                        throw ex;
                    }
                });
            }
        }

        public void AllowBreak()
        {
            if (requestBreakEvent.WaitOne(0))
            {
                Break();
            }
        }

        public void RequestBreak()
        {
            // set pre-warning indicator
            requestBreakEvent.Set();
        }

        public void Abort()
        {
            abortEvent.Set();
        }

        public void Resume()
        {
            resumeEvent.Set();
        }

        public void Break()
        {
            breakEvent.Set();
        }

        public void SetBreakpoint(int line)
        {
            lock (this)
            {
                breakpoints.Add(line);
            }
        }

        public void ClearBreakpoint(int line)
        {
            lock (this)
            {
                breakpoints.Remove(line);
            }
        }

        public void ClearAllBreakpoints()
        {
            lock (this)
            {
                breakpoints.Clear();
            }
        }
    }

}
