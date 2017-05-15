using IronPython.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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
