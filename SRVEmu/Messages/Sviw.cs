﻿namespace SRVEmu.Messages
{
    public class Sviw : AbstractMessage
    {
        public override string _Name { get => "sviw"; }

        public string? PERS { get; set; }

        public override void Process(AbstractDirtySockServer context, DirtySockClient client)
        {
            var mc = context as MatchmakerServer;
            if (mc == null) return;

            Model.User? user = client.User;
            if (user == null || user.SelectedPersona != -1) return;
            user.SelectPersona(PERS);
            if (user.SelectedPersona == -1) return; //failed?
            client.SendMessage(new PersOut()
            {
                NAME = user.Username,
                PERS = user.PersonaName
            });

            client.SendMessage(new Ping()
            {

            });
        }
    }
}
