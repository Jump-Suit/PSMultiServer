﻿using PSMultiServer.Addons.Medius.RT.Models;
using PSMultiServer.Addons.Medius.MEDIUS.Medius.Models;

namespace PSMultiServer.Addons.Medius.MEDIUS.PluginArgs
{
    public class OnAccountLoginRequestArgs
    {
        /// <summary>
        /// Player making request.
        /// </summary>
        public ClientObject Player { get; set; }
        /// <summary>
        /// AccountLogin request.
        /// </summary>
        public IMediusRequest Request { get; set; }

        public override string ToString()
        {
            return base.ToString() + " " +
                $"Player: {Player} " +
                $"Request: {Request}";
        }
    }
}
