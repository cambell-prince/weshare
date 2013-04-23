using System;
using System.Linq;

namespace WeShare.Transport
{
    public interface IHgTransport
    {
        void Push();
        bool Pull();
        void Clone();
    }
}
