using System;
using System.Runtime.Serialization;

namespace ZeroMQ
{
    /// <summary>
    /// An exception thrown by the result of libzmq.
    /// </summary>
    [Serializable]
    public class ZException : Exception
    {
        private ZError? _error;

        public ZError? Error => _error;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        protected ZException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        public ZException(ZError? errorSymbol)
            : this(errorSymbol, default, default) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        public ZException(ZError? errorSymbol, string message)
            : this(errorSymbol, message, default) { }

        public ZException(ZError? errorSymbol, string message, Exception inner)
            : base(MakeMessage(errorSymbol, message), inner)
            => _error = errorSymbol;

        static string MakeMessage(ZError? error, string additionalMessage)
            => error != null
                ? string.IsNullOrEmpty(additionalMessage)
                    ? error.ToString()
                    : $"{error}: {additionalMessage}"
                : additionalMessage;

        public override string ToString()
            => Message;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZException"/> class.
        /// </summary>
        /// <param name="info"><see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context"><see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected ZException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
