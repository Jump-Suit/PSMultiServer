﻿namespace MultiSocks.DirtySocks.Messages
{
    public class PgetOut : AbstractMessage
    {
        public override string _Name { get => "PGET"; }
        public string? USER { get; set; }
    }
}
