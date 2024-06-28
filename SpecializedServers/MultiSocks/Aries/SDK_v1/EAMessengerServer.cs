﻿using MultiSocks.Aries.DataStore;
using MultiSocks.Aries.SDK_v1.Messages;
using MultiSocks.Aries.SDK_v1.Model;

namespace MultiSocks.Aries.SDK_v1
{
    public class EAMessengerServer : AbstractAriesServerV1
    {
        public override Dictionary<string, Type?> NameToClass { get; } =
            new Dictionary<string, Type?>()
            {
                { "AUTH", typeof(EAMAuth) },
                { "RGET", typeof(Rget) },
                { "PSET", typeof(Pset) },
            };

        public UserCollection Users = new();

        private readonly Thread PingThread;

        public EAMessengerServer(ushort port, string listenIP, string? Project = null, string? SKU = null, bool secure = false, string CN = "", string email = "", bool WeakChainSignedRSAKey = false) : base(port, listenIP, Project, SKU, secure, CN, email, WeakChainSignedRSAKey)
        {
            lock (Users)
                Users.AddUser(new Model.User() { ID = 24 }); // Admin player.

            PingThread = new Thread(PingLoop);
            PingThread.Start();
        }

        public void PingLoop()
        {
            while (true)
            {
                Broadcast(new Ping());
                Thread.Sleep(30000);
            }
        }

        public override void RemoveClient(AriesClient client)
        {
            base.RemoveClient(client);

            //clean up this user's state.
            //are they logged in?
            Model.User? user = client.User;
            if (user != null)
            {
                Users.RemoveUser(user);

            }
        }

        public void TryEAMLogin(DbAccount user, AriesClient client)
        {
            //is someone else already logged in as this user?
            Model.User? oldUser = Users.GetUserByName(user.Username);
            if (oldUser != null)
            {
                oldUser.Connection?.Close(); //should remove the old user.
                Thread.Sleep(500);
            }

            string[] personas = new string[4];
            for (int i = 0; i < user.Personas.Count; i++)
            {
                personas[i] = user.Personas[i];
            }

            //make a user object from DB user
            User user2 = new()
            {
                Connection = client,
                ID = user.ID,
                Personas = personas,
                Username = user.Username,
                ADDR = client.ADDR,
                LADDR = client.LADDR
            };

            Users.AddUser(user2);
            client.User = user2;

            // Ideally all the infos comes from User profile and not hardcodded.


            client.SendMessage(new EAMAuthOut()
            {
                BORN = "19800325",
                GEND = "M",
                FROM = "US",
                LANG = "en",
                LAST = "2003.12.8 15:51:38",
                TOS = user.TOS,
                NAME = user.Username,
                MAIL = user.MAIL,
                PERSONAS = string.Join(',', user.Personas)
            });
        }
    }
}
