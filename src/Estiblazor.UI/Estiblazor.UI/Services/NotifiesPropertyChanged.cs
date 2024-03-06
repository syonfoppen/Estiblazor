using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Estiblazor.UI.Services
{
    public class NotifiesPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void PropertyChange<T>(ref T current, T newValue, [CallerMemberName] string? memberName = null)
        {
            if (Equals(current, newValue)) return;
            current = newValue;
            OnPropertyChanged(memberName);
        }

        protected void OnPropertyChanged(string? memberName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
        }
    }
}
