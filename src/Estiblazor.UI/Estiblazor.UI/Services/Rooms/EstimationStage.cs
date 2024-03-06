using Estiblazor.UI.Services.Users;
using System.Collections.ObjectModel;

namespace Estiblazor.UI.Services.Rooms
{

    public class EstimationStage : NotifiesPropertyChanged
    {
        private string name = string.Empty;
        public string Name { get => name; set => PropertyChange(ref name, value); }

        private ObservableCollection<UserChoice> userChoices = [];
        public ObservableCollection<UserChoice> UserChoices { get => userChoices; set => PropertyChange(ref userChoices, value); }

        private string[] availableChoices = [];
        public string[] AvailableChoices { get => availableChoices; set => PropertyChange(ref availableChoices, value); }

        public event EventHandler<ChoiceChangedEventArgs>? ChoiceChanged;

        private bool isRevealed;
        public bool IsRevealed { get => isRevealed; set => PropertyChange(ref isRevealed, value); }

        public void Reveal()
        {
            IsRevealed = true;
        }

        public void Reset()
        {
            IsRevealed = false;
            while(userChoices.Count > 0)
            {
                RemoveChoice(userChoices[^1]);
            }
        }

        public string? GetChoice(UserId userId)
        {
            if (UserChoices.FirstOrDefault(x => x.UserId == userId) is { } y)
            {
                return y.Choice;
            }
            return null;
        }

        public void RemoveChoice(UserId userId)
        {
            var choice = UserChoices.FirstOrDefault(x => x.UserId == userId);
            if (choice != null)
            {
                RemoveChoice(choice);
            }
        }

        private void RemoveChoice(UserChoice choice)
        {
            userChoices.Remove(choice);
            ChoiceChanged?.Invoke(this, new ChoiceChangedEventArgs(choice.UserId, null));
        }

        public void SetChoice(UserId userId, string choice)
        {
            if (userChoices.FirstOrDefault(x => x.UserId == userId) is { } y)
            {
                y.Choice = choice;
            }
            else
            {
                userChoices.Add(new UserChoice { Choice = choice, UserId = userId });
            }
            ChoiceChanged?.Invoke(this, new ChoiceChangedEventArgs(userId, choice));
        }
    }
}
