﻿using PSMultiServer.Addons.Medius.MEDIUS.Medius.Models;

namespace PSMultiServer.Addons.Medius.MEDIUS.PluginArgs
{
    public class OnPartyArgs
    {
        /// <summary>
        /// Party.
        /// </summary>
        public Party Party { get; set; }

        public override string ToString()
        {
            return base.ToString() + " " +
                $"Party: {Party}";
        }
    }
}
