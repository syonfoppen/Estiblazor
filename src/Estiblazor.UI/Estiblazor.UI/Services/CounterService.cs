namespace Estiblazor.UI.Services
{
    public class CounterService
    {
        private int count;

        public int Count
        {
            get => count;
            set
            {
                count = value;
                CountChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler CountChanged;
    }
}
