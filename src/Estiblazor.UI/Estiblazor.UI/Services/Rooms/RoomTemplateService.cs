using Estiblazor.UI.Enums;

namespace Estiblazor.UI.Services.Rooms
{
    public class RoomTemplateService : IRoomTemplateService
    {
        private readonly ICollection<EstimationStage> PlanningPoker;

        private readonly ICollection<EstimationStage> OneTillTen;

        private readonly ICollection<EstimationStage> LikeDislike;

        public RoomTemplateService()
        {
            // define PlanningPoker
            PlanningPoker =
            [
                new EstimationStage
                {
                    Name = "effort",
                    AvailableChoices = ["0.5", "1", "2", "3", "5", "8", "13", "20", "<i class=\"fa-solid fa-infinity\"></i>"],
                    IsRevealed = false,
                    UserChoices = [],
                },

                new EstimationStage
                {
                    Name = "complexity",
                    AvailableChoices = ["S", "M", "L", "<i class=\"fa-solid fa-infinity\"></i>"],
                    IsRevealed = false,
                    UserChoices = []
                }
            ];


            //Define OneTillTen
            OneTillTen = [
                new EstimationStage
                {
                    Name = "Score",
                    AvailableChoices = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"],
                    IsRevealed = false,
                    UserChoices = [],
                }
            ];

            //Define LikeDislike
            LikeDislike = [
                new EstimationStage
                {
                    Name = "Like",
                    AvailableChoices = ["<i class=\"fa-solid fa-thumbs-up\"></i>", "<i class=\"fa-solid fa-thumbs-down\"></i>"],
                    IsRevealed = false,
                    UserChoices = []
                }
            ];
        }

        public ICollection<EstimationStage> GetRoomTemplate(RoomTemplates roomTemplate)
        {
            switch (roomTemplate)
            {
                case RoomTemplates.Planning_Poker:
                    return PlanningPoker;
                case RoomTemplates.One_till_ten:
                    return OneTillTen;
                case RoomTemplates.LIKE_DISLIKE:
                    return LikeDislike;
                default:
                    return new List<EstimationStage>();
            }
        }
    }
}
