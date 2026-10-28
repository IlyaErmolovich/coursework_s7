using System;
using System.Collections.Generic;
using System.Text;
namespace FrostDuel {
    // RFCOMM is a stream, not a message transport. Split/coalesced UTF-8 reads are normal.
    public sealed class LineFramer {
        const int MaxBytes=262144;
        readonly List<byte> pending=new List<byte>();
        public void Clear()=>pending.Clear();
        public void Feed(byte[] bytes,Action<string> receive) {
            foreach(byte b in bytes) {
                if(b==10) {string line=Encoding.UTF8.GetString(pending.ToArray());pending.Clear();if(line.Length>0)receive(line);}
                else {if(pending.Count>=MaxBytes){pending.Clear();throw new InvalidOperationException("Слишком большое сообщение Bluetooth");}pending.Add(b);}
            }
        }
    }
}
