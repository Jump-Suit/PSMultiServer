using MultiSocks.Aries.Messages;
using System.Collections.Specialized;

namespace MultiSocks.Aries.Model
{
    public class UserCollection
    {
        protected OrderedDictionary Users = new();

        public List<AriesUser> GetAll()
        {
            lock (Users)
                return Users.Values.Cast<AriesUser>().ToList();
        }

        public virtual bool AddUser(AriesUser? user, string VERS = "")
        {
            if (user == null)
                return false;

            lock (Users)
            {
                if (Users.Contains(user.ID))
                    return false;

                Users.Add(user.ID, user);
                return true;
            }
        }

        public virtual bool AddUserWithRoomMesg(AriesUser? user, string VERS = "")
        {
            if (user == null)
                return false;

            lock (Users)
            {
                if (Users.Contains(user.ID))
                    return false;

                Users.Add(user.ID, user);
                return true;
            }
        }

        public virtual bool RemoveUser(AriesUser? user)
        {
            if (user == null)
                return false;

            lock (Users)
            {
                if (!Users.Contains(user.ID))
                    return false;

                Users.Remove(user.ID);
                return true;
            }
        }

        public AriesUser? GetUserByName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            lock (Users)
                return Users.Values.Cast<AriesUser>().FirstOrDefault(x => x.Username == name);
        }

        public AriesUser? GetUserByPersonaName(string name)
        {
            lock (Users)
                return Users.Values.Cast<AriesUser>().FirstOrDefault(x => x.PersonaName == name);
        }

        public int Count()
        {
            lock (Users)
                return Users.Count;
        }

        public void Broadcast(AbstractMessage msg)
        {
            lock (Users)
            {
                foreach (AriesUser user in Users.Values)
                {
                    if (user.Connection == null)
                    {
                        new Thread(() =>
                        {
                            int retries = 0;
                            while (retries < 5)
                            {
                                if (user.Connection != null)
                                {
                                    user.Connection.SendMessage(msg);
                                    break;
                                }
                                retries++;
                                Thread.Sleep(500);
                            }
                        }).Start();
                    }
                    else
                        user.Connection.SendMessage(msg);
                }
            }
        }
    }
}
