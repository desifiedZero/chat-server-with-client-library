using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatLibrary
{
    [Serializable]
    public class InitialMessage
    {
        public List<string> Clients { get; set; }
        public string Guid { get; set; }

        public InitialMessage(List<string> clients, string guid)
        {
            this.Clients = clients;
            this.Guid = guid;
        }
    }
}
