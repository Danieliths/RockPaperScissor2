using Newtonsoft.Json;
using System;


namespace RockPaperScissor
{
    public class Player
    {
        //byta till fancykey som ID
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public Game.PlayerType TypeOfPlayer { get; set; }

        public Player(string name, Game.PlayerType playertype)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            TypeOfPlayer = playertype;
        }
    }   
}
  