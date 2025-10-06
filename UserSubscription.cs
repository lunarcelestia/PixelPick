using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelPick
{
    public class UserSubscription
    {
        public long UserId { get; set; }
        public bool IsSubscribed { get; set; }
        public List<string> Interests { get; set; } = new List<string>();
    }
}
