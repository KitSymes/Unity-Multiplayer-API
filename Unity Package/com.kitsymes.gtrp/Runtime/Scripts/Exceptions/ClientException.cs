using System;

namespace KitSymes.GTRP
{
    public class ClientException : Exception
    {
        public ClientException() : base() { }
        public ClientException(string message) : base(message) { }
        public ClientException(string message, Exception inner) : base(message, inner) { }
    }
}
