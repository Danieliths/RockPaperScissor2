using Newtonsoft.Json;
using System;
using System.Collections.Generic;
namespace RockPaperScissor
{
    public class Game
    {
        public enum PlayerType { Creator, Joiner, None}
        public enum Move { Rock, Paper, Scissors }
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string GameName { get; set; }
        public string CreatorName { get; set; }
        public string JoinerName { get; set; }
        public List<Move> CreatorMove { get; set; }
        public List<Move> JoinerMove { get; set; }
        public int CreatorScore { get; set; }
        public int JoinerScore { get; set; }
        public bool IsGameCompleted { get; set; }
        public bool IsJoinable { get; set; }
        public PlayerType ToMove { get; set; }
        public int Turn { get; set; }
        public int FirstToNumberOfWins { get; set; }
        public Game(string hostName)
        {
            Id = Guid.NewGuid().ToString();
            GameName = hostName + Id;
            CreatorName = hostName;
            JoinerName = "Empty";
            CreatorMove = new List<Move>();
            JoinerMove = new List<Move>();
            CreatorScore = 0;
            JoinerScore = 0;
            IsGameCompleted = true;
            Turn = 1;
            ToMove = PlayerType.Creator;
            FirstToNumberOfWins = 2;
            IsJoinable = true;
        }
    }

    
    //public override string ToString()
    //{
    //    return JsonConvert.SerializeObject(this);
    //}

}