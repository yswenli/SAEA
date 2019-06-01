﻿/* Copyright 2018-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using SAEA.Mongo.Driver.Core.Misc;

namespace SAEA.Mongo.Driver.Core.WireProtocol.Messages.Encoders.JsonEncoders
{
    /// <summary>
    /// Represents a Json encoder for a CommandResponseMessage.
    /// </summary>
    /// <seealso cref="SAEA.Mongo.Driver.Core.WireProtocol.Messages.Encoders.IMessageEncoder" />
    public class CommandResponseMessageJsonEncoder : IMessageEncoder
    {
        // private fields
        private readonly CommandMessageJsonEncoder _wrappedEncoder;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandResponseMessageJsonEncoder" /> class.
        /// </summary>
        /// <param name="wrappedEncoder">The wrapped encoder.</param>
        public CommandResponseMessageJsonEncoder(CommandMessageJsonEncoder wrappedEncoder)
        {
            _wrappedEncoder = Ensure.IsNotNull(wrappedEncoder, nameof(wrappedEncoder));
        }

        // public methods
        /// <summary>
        /// Reads the message.
        /// </summary>
        /// <returns>A message.</returns>
        public CommandResponseMessage ReadMessage()
        {
            var wrappedMessage = (CommandMessage)_wrappedEncoder.ReadMessage();
            return new CommandResponseMessage(wrappedMessage);
        }

        /// <summary>
        /// Writes the message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void WriteMessage(CommandResponseMessage message)
        {
            var wrappedMessage = message.WrappedMessage;
            _wrappedEncoder.WriteMessage(wrappedMessage);
        }

        // explicit interface implementations
        MongoDBMessage IMessageEncoder.ReadMessage()
        {
            return ReadMessage();
        }

        void IMessageEncoder.WriteMessage(MongoDBMessage message)
        {
            WriteMessage((CommandResponseMessage)message);
        }
    }
}