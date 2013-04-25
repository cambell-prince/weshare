using System;
using System.Linq;

namespace WeShare.Transport
{
    public interface IWeShareTransport
    {
        void Push();
        bool Pull();
        void Clone();
    }
}
