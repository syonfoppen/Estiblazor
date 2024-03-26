using Estiblazor.UI.Enums;

namespace Estiblazor.UI.Services.Rooms
{
    public class RoomCreationService : IRoomCreationService
    {
        private readonly NewRoomModel _newRoomModel;

        private readonly ICollection<NewStageModel> PlanningPoker;

        private readonly ICollection<NewStageModel> OneTillTen;

        private readonly ICollection<NewStageModel> LikeDislike;
        private readonly IRoomCollection roomCollection;

        public RoomCreationService(IRoomCollection roomCollection)
        {
            _newRoomModel = new NewRoomModel();

            #region define templates
            // define PlanningPoker
            PlanningPoker =
            [
                new NewStageModel
                {
                    StageName = "effort",
                    AvailableChoices =  [
                        new AddAvailableChoiceViewModel() { ChoiceName = "0.5" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "1" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "2" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "3" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "4" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "5" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "8" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "13" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "20" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "<i class=\"fa-solid fa-infinity\"></i>" }]
                },

                new NewStageModel
                {
                    StageName = "complexity",
                    AvailableChoices = [
                        new AddAvailableChoiceViewModel() { ChoiceName = "S" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "M" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "L" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "<i class=\"fa-solid fa-infinity\"></i>" }]
                }
            ];


            //Define OneTillTen
            OneTillTen = [
                new NewStageModel
                {
                    StageName = "Score",
                    AvailableChoices = [
                        new AddAvailableChoiceViewModel() { ChoiceName = "1" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "2" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "3" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "4" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "5" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "6" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "7" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "8" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "9" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "10" }]
                }
            ];

            //Define LikeDislike
            LikeDislike = [
                new NewStageModel
                {
                    StageName = "Like",
                    AvailableChoices = [
                        new AddAvailableChoiceViewModel() { ChoiceName = "<i class=\"fa-solid fa-thumbs-up\"></i>" },
                        new AddAvailableChoiceViewModel() { ChoiceName = "<i class=\"fa-solid fa-thumbs-down\"></i>" }]
                }
            ];
            this.roomCollection = roomCollection;
            #endregion
        }

        public IEnumerable<KeyValuePair<Guid, string>> GetStages()
        {
            return _newRoomModel.Stages.Select(s => new KeyValuePair<Guid, string>(s.StageId, s.StageName));
        }

        public void AddNewStage(string stageName)
        {
            var stage = new NewStageModel { StageName = stageName };

            if (!_newRoomModel.Stages.Any(a => a.IsSelected))
            {
                stage.IsSelected = true;
            }
            _newRoomModel.Stages.Add(stage);
        }

        public void AddNewChoice(string choice)
        {
            var selectedStage = GetSelectedStage();
            if (selectedStage == null)
            {
                return;
            }
            var newChoice = new AddAvailableChoiceViewModel { ChoiceName = choice };

            selectedStage.AvailableChoices.Add(newChoice);
        }

        public void RemoveChoice(Guid choiceId)
        {
            var selectedStage = GetSelectedStage();
            if (selectedStage == null)
            {
                return;
            }

            var choiceToDelete = selectedStage.AvailableChoices.FirstOrDefault(x => x.ChoiceId == choiceId);

            if (choiceToDelete == null)
            {
                return;
            }

            selectedStage.AvailableChoices.Remove(choiceToDelete);
        }

        public NewStageModel? GetSelectedStage()
        {
            return _newRoomModel.Stages.FirstOrDefault(x => x.IsSelected);
        }

        public bool IsSelectedStage(Guid stageId)
        {
            var stage = _newRoomModel.Stages.FirstOrDefault(x => x.StageId == stageId);
            return stage?.IsSelected ?? false;
        }

        public void SetSelectedStage(Guid stageId)
        {
            var selectedStage = _newRoomModel.Stages.FirstOrDefault(x => x.IsSelected);
            if (selectedStage != null)
            {
                selectedStage.IsSelected = false;
            }

            var stage = _newRoomModel.Stages.FirstOrDefault(x => x.StageId == stageId);
            if (stage == null)
            {
                return;
            }
            stage.IsSelected = true;
        }

        public void SetRoomTemplate(RoomTemplates roomTemplate)
        {
            switch (roomTemplate)
            {
                case RoomTemplates.Planning_Poker:
                    _newRoomModel.Stages = PlanningPoker;
                    break;
                case RoomTemplates.One_till_ten:
                    _newRoomModel.Stages = OneTillTen;
                    break;
                case RoomTemplates.LIKE_DISLIKE:
                    _newRoomModel.Stages = LikeDislike;
                    break;
                default:
                    break;
            }
        }
        public string CreateRoom()
        {
            string roomid = GetRandomRoomName();
            while (roomCollection.GetExistingRoom(roomid) is not null)
            {
                roomid = GetRandomRoomName();
            }

            var stages = from stage in _newRoomModel.Stages
                         select new EstimationStage
                         {
                             AvailableChoices = stage.AvailableChoices.Select(x => x.ChoiceName).ToArray(),
                             IsRevealed = false,
                             Name = stage.StageName,
                         };

            var vm = new RoomViewModel(stages)
            {
                Name = roomid,
                Id = new RoomId(roomid),
            };

            roomCollection.AddNewRoom(vm);

            return roomid;
        }

        private static string GetRandomRoomName()
        {
            var id = Random.Shared.Next(10000, 99999 + 1).ToString();
            return id;
        }
    }
}
