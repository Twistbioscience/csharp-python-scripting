using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Twist.PyCon
{
    public class ConsoleViewModel : ViewModelBase
    {
        #region Properties
        private string _consoleInput = string.Empty;
        private ObservableCollection<string> _consoleOutput = new ObservableCollection<string>() { "Python console initialized..." };

        public string ConsoleInput
        {
            get { return _consoleInput; }
            set
            {
                _consoleInput = value;
                OnPropertyChanged("ConsoleInput");
            }
        }

        public ObservableCollection<string> ConsoleOutput
        {
            get { return _consoleOutput; }
            set
            {
                _consoleOutput = value;
                OnPropertyChanged("ConsoleOutput");
            }
        }
        #endregion

        private ScriptEngine _engine;
        private ScriptScope _scope;
        private MemoryStream _outputStream;

        public ConsoleViewModel()
        {
            _engine = Python.CreateEngine();
            _scope = _engine.CreateScope();
            SetupOutputStream();
        }

        private void SetupOutputStream()
        {
            _outputStream = new MemoryStream();
            _engine.Runtime.IO.SetOutput(_outputStream, Encoding.UTF8);
        }

        public void AppendGlobal(string key, dynamic value)
        {
            _engine.GetBuiltinModule().SetVariable(key, value);
        }

        public async Task RunCommand()
        {
            try
            {
                ConsoleOutput.Add(_consoleInput);
                await Task.Run(() => _engine.Execute(_consoleInput, _scope));
                ReadOutput();
            }
            catch (Exception e)
            {
                ConsoleOutput.Add(e.ToString());
            }
            ConsoleInput = string.Empty;
        }

        private void ReadOutput()
        {
            //warning: hacky
            var output = Encoding.UTF8.GetString(_outputStream.ToArray()).TrimEnd(new char[] { '\r', '\n' });
            _outputStream.SetLength(0);
            ConsoleOutput.Add(output);
        }
    }
}
