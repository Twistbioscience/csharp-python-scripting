using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Twist.PyCon
{
    public class MainViewModel : ViewModelBase
    {

        private PythonScript _script;
        public PythonScript Script
        {
            get { return _script; }
            set
            {
                _script = value;
                OnPropertyChanged(nameof(Script));
            }
        }
        public PythonScriptUnit ScriptUnit { get; set; }

        public RelayCommand<object> ExecuteScriptCommand { get; private set; }
        public RelayCommand<object> PauseCommand { get; private set; }
        public RelayCommand<object> ResumeCommand { get; private set; }

        public ConsoleViewModel ConsoleVM { get; private set; }

        public Device DeviceA { get; private set; }
        public Device DeviceB { get; private set; }

        public MainViewModel()
        {

            ExecuteScriptCommand = new RelayCommand<object>(async _ => await Task.Run(() =>
            {
                ScriptUnit.Execute();
            }));

            PauseCommand = new RelayCommand<object>(async _ => await Task.Run(() =>
            {
                ScriptUnit.Break();
            }));


            ResumeCommand = new RelayCommand<object>(async _ => await Task.Run(() =>
            {
                ScriptUnit.Resume();
            }));

            Script = new PythonScript("./script.py");
            ScriptUnit = new PythonScriptUnit(Script, true);
            ConsoleVM = new ConsoleViewModel();

            DeviceA = new Device();
            DeviceB = new Device();
            ConsoleVM.AppendGlobal("devA", DeviceA);
            ConsoleVM.AppendGlobal("devB", DeviceB);
            ScriptUnit.AppendGlobal("devA", DeviceA);
            ScriptUnit.AppendGlobal("devB", DeviceB);
        }
    }


    public class ViewModelBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetPropertyAndNotify<T>(ref T existingValue, T newValue, string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(existingValue, newValue))
            {
                return false;
            }

            existingValue = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            return true;
        }
        #endregion
    }
}
