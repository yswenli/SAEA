/* Copyright 2013-present MongoDB Inc.
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

using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Mongo.Bson;
using SAEA.Mongo.Bson.Serialization.Serializers;
using SAEA.Mongo.Driver.Core.Bindings;
using SAEA.Mongo.Driver.Core.Misc;
using SAEA.Mongo.Driver.Core.WireProtocol.Messages.Encoders;

namespace SAEA.Mongo.Driver.Core.Operations
{
    /// <summary>
    /// Represents a ping operation.
    /// </summary>
    public class PingOperation : IReadOperation<BsonDocument>
    {
        // fields
        private MessageEncoderSettings _messageEncoderSettings;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="PingOperation"/> class.
        /// </summary>
        /// <param name="messageEncoderSettings">The message encoder settings.</param>
        public PingOperation(MessageEncoderSettings messageEncoderSettings)
        {
            _messageEncoderSettings = messageEncoderSettings;
        }

        // properties
        /// <summary>
        /// Gets the message encoder settings.
        /// </summary>
        /// <value>
        /// The message encoder settings.
        /// </value>
        public MessageEncoderSettings MessageEncoderSettings
        {
            get { return _messageEncoderSettings; }
        }

        // methods
        internal BsonDocument CreateCommand()
        {
            return new BsonDocument
            {
                { "ping", 1 }
            };
        }

        private ReadCommandOperation<BsonDocument> CreateOperation()
        {
            var command = CreateCommand();
            return new ReadCommandOperation<BsonDocument>(DatabaseNamespace.Admin, command, BsonDocumentSerializer.Instance, _messageEncoderSettings)
            {
                RetryRequested = false
            };
        }

        /// <inheritdoc/>
        public BsonDocument Execute(IReadBinding binding, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(binding, nameof(binding));
            var operation = CreateOperation();
            return operation.Execute(binding, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<BsonDocument> ExecuteAsync(IReadBinding binding, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(binding, nameof(binding));
            var operation = CreateOperation();
            return await operation.ExecuteAsync(binding, cancellationToken).ConfigureAwait(false);
        }
    }
}
