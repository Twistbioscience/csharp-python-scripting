using System.Collections.Generic;
using System.ComponentModel;

namespace Twist.PyCon
{
    public class MainViewModel : ViewModelBase
    {
        public ConsoleViewModel ConsoleVM { get; private set; }

        public Device DeviceA { get; private set; }
        public Device DeviceB { get; private set; }

        public MainViewModel()
        {
            ConsoleVM = new ConsoleViewModel();
            DeviceA = new Device();
            DeviceB = new Device();
            ConsoleVM.AppendGlobal("devA", DeviceA);
            ConsoleVM.AppendGlobal("devB", DeviceB);
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
