using Horizon.MUM.Models;

namespace Horizon.DME.PluginArgs
{
    public class OnPlayerArgs
    {
        public ClientObject? Player { get; set; }

        public World? Game { get; set; }
    }
}