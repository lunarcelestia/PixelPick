using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelPick
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public long ChatId { get; set; }
        public List<Subscription> Subscriptions { get; set; }
        public List<string> SearchHistory { get; set; }
        public List<string> Interests { get; set; }
        public bool IsSubscribed { get; set; }
    }

    public class SteamGame
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
    }

    public class Subscription
    {
        public int Id { get; set; }
        public string GameName { get; set; }
    }

}
