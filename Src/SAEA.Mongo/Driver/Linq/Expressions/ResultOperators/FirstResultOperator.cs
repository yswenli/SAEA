/* Copyright 2015-present MongoDB Inc.
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
using System.Linq.Expressions;
using SAEA.Mongo.Bson.Serialization;
using SAEA.Mongo.Driver.Core.Misc;

namespace SAEA.Mongo.Driver.Linq.Expressions.ResultOperators
{
    internal sealed class FirstResultOperator : ResultOperator, IResultTransformer
    {
        private readonly IBsonSerializer _serializer;
        private readonly Type _type;

        private readonly bool _isDefault;

        public FirstResultOperator(Type type, IBsonSerializer serializer, bool isDefault)
        {
            _type = Ensure.IsNotNull(type, nameof(type));
            _serializer = Ensure.IsNotNull(serializer, nameof(serializer));
            _isDefault = isDefault;
        }

        public override string Name
        {
            get { return _isDefault ? "FirstOrDefault" : "First"; }
        }

        public override IBsonSerializer Serializer
        {
            get { return _serializer; }
        }

        public override Type Type
        {
            get { return _type; }
        }

        public LambdaExpression CreateAggregator(Type sourceType)
        {
            return ResultTransformerHelper.CreateAggregator(Name, sourceType);
        }

        public LambdaExpression CreateAsyncAggregator(Type sourceType)
        {
            return ResultTransformerHelper.CreateAsyncAggregator(Name + "Async", sourceType);
        }
    }
}
