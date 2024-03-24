namespace Estiblazor.UI.Services.Rooms
{
    public class AddAvailableChoiceViewModel
    {
        public Guid ChoiceId { get; set; } = Guid.NewGuid();
        public string ChoiceName { get; set; } = "";
    }
}
