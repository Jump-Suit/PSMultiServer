﻿namespace MultiSocks.DirtySocks.Messages
{
    public class PlusKik : AbstractMessage
    {
        public override string _Name { get => "+kik"; }

        public string? REASON { get; set; }
    }
}
