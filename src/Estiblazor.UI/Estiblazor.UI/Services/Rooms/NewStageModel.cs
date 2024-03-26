namespace Estiblazor.UI.Services.Rooms
{
    public class NewStageModel
    {
        public Guid StageId { get; set; } = Guid.NewGuid();
        public string StageName { get; set; } = "";
        public bool IsSelected { get; set; } = false;

        public ICollection<AddAvailableChoiceViewModel> AvailableChoices { get; set; } = [];
    }
}
