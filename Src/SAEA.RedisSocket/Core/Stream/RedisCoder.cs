/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisCoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/12 14:44:08
*描述：
*=====================================================================
*修改时间：2021/1/12 14:44:08
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Core.Stream;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// 分析redis stream 内容
    /// </summary>
    internal partial class RedisCoder
    {
        /// <summary>
        /// 分析redis stream 内容
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public ResponseData<IEnumerable<StreamEntry>> GetStreamData(string command, CancellationToken ctoken)
        {
            ResponseData<IEnumerable<StreamEntry>> responseData = new ResponseData<IEnumerable<StreamEntry>>();

            var list = new List<StreamEntry>();

            var row1 = GetRowNum(command, out string error);
            if (!string.IsNullOrEmpty(error))
            {
                responseData.Type = ResponseType.Error;
                responseData.Data = error;
                return responseData;
            }
            if (row1 <= 0)
            {
                responseData.Type = ResponseType.Empty;
                responseData.Data = "";
                return responseData;
            }
            for (int i = 0; i < row1; i++)
            {
                _ = GetRowNum(GetRedisReplyLine(ctoken), out error);
                if (!string.IsNullOrEmpty(error))
                {
                    responseData.Type = ResponseType.Error;
                    responseData.Data = error;
                    return responseData;
                }

                _ = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                if (!string.IsNullOrEmpty(error))
                {
                    responseData.Type = ResponseType.Error;
                    responseData.Data = error;
                    return responseData;
                }

                var entity = new StreamEntry();
                entity.Topic = GetRedisReplyLine(ctoken);
                var idFileds = new List<IdFiled>();

                var row2 = GetRowNum(GetRedisReplyLine(ctoken), out error);

                for (int j = 0; j < row2; j++)
                {
                    _ = GetRowNum(GetRedisReplyLine(ctoken), out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = error;
                        return responseData;
                    }
                    _ = GetWordsNum(GetRedisReplyLine(ctoken), out error);

                    var idFiled = new IdFiled();
                    idFiled.RedisID = new RedisID(GetRedisReplyLine(ctoken));

                    var flist = new List<RedisField>();

                    var row3 = GetRowNum(GetRedisReplyLine(ctoken), out error);

                    RedisField redisField = null;

                    for (int k = 0; k < row3; k++)
                    {
                        _ = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                        switch (k)
                        {
                            case 0:
                                redisField = new RedisField();
                                redisField.Field = GetRedisReplyLine(ctoken);
                                break;
                            case 1:
                                redisField.String = GetRedisReplyLine(ctoken);
                                flist.Add(redisField);
                                break;
                            default:
                                if (k >= 2)
                                {
                                    if (k % 2 == 0)
                                    {
                                        redisField = new RedisField();
                                        redisField.Field = GetRedisReplyLine(ctoken);
                                    }
                                    else
                                    {
                                        redisField.String = GetRedisReplyLine(ctoken);
                                        flist.Add(redisField);
                                    }
                                }
                                break;
                        }
                    }
                    idFiled.RedisFields = flist;
                    idFileds.Add(idFiled);
                }
                entity.IdFileds = idFileds;
                list.Add(entity);
            }
            responseData.Entity = list;
            return responseData;
        }

        /// <summary>
        /// GetStreamRangeData,
        /// XRANGE
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public ResponseData<IEnumerable<IdFiled>> GetStreamRangeData(string command, CancellationToken ctoken)
        {
            ResponseData<IEnumerable<IdFiled>> responseData = new ResponseData<IEnumerable<IdFiled>>();

            var list = new List<IdFiled>();

            var row1 = GetRowNum(command, out string error);
            if (!string.IsNullOrEmpty(error))
            {
                responseData.Type = ResponseType.Error;
                responseData.Data = error;
                return responseData;
            }
            if (row1 <= 0)
            {
                responseData.Type = ResponseType.Empty;
                responseData.Data = "";
                return responseData;
            }

            for (int i = 0; i < row1; i++)
            {
                _ = GetRowNum(command, out error);

                _ = GetWordsNum(GetRedisReplyLine(ctoken), out error);

                var idFiled = new IdFiled();
                idFiled.RedisID = new RedisID(GetRedisReplyLine(ctoken));

                var flist = new List<RedisField>();

                var row2 = GetRowNum(command, out error);

                RedisField redisField = null;

                for (int k = 0; k < row2; k++)
                {
                    _ = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                    switch (k)
                    {
                        case 0:
                            redisField = new RedisField();
                            redisField.Field = GetRedisReplyLine(ctoken);
                            break;
                        case 1:
                            redisField.String = GetRedisReplyLine(ctoken);
                            flist.Add(redisField);
                            break;
                        default:
                            if (k >= 2)
                            {
                                if (k % 2 == 0)
                                {
                                    redisField = new RedisField();
                                    redisField.Field = GetRedisReplyLine(ctoken);
                                }
                                else
                                {
                                    redisField.String = GetRedisReplyLine(ctoken);
                                    flist.Add(redisField);
                                }
                            }
                            break;
                    }

                    idFiled.RedisFields = flist;
                }
                list.Add(idFiled);
            }
            responseData.Entity = list;
            return responseData;
        }

        /// <summary>
        /// 解析从redis返回的命令
        /// </summary>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public ResponseData<IEnumerable<StreamEntry>> Decoder(CancellationToken ctoken)
        {
            var responseData = new ResponseData<IEnumerable<StreamEntry>>();

            try
            {
                string command = GetRedisReplyLine(ctoken);

                if (string.IsNullOrEmpty(command)) return null;

                if (command.IndexOf("-") == 0 && command.IndexOf(MOVED) == -1)
                {
                    responseData.Type = ResponseType.Error;
                    responseData.Data = command;
                    return responseData;
                }

                while (command == ConstHelper.ENTER)
                {
                    command = GetRedisReplyLine(ctoken);

                    if (command.IndexOf("-") == 0 && command.IndexOf(MOVED) == -1)
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = command;
                        return responseData;
                    }
                    if (string.IsNullOrEmpty(command)) return null;
                }

                var temp = Redirect<IEnumerable<StreamEntry>>(command);

                if (temp != null)
                {
                    responseData = temp;
                }
                else
                {
                    if (command.IndexOf("-") == 0)
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = command;
                        return responseData;
                    }
                    responseData.Type = ResponseType.Lines;
                    responseData = GetStreamData(command, ctoken);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("RedisCoder.Decoder", ex);
                responseData.Type = ResponseType.Error;
                responseData.Data = "操作超时";
            }
            return responseData;
        }

        /// <summary>
        /// 解析从redis返回的命令
        /// </summary>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public ResponseData<IEnumerable<IdFiled>> StreamRangeDecoder(CancellationToken ctoken)
        {
            var responseData = new ResponseData<IEnumerable<IdFiled>>();

            try
            {
                string command = string.Empty;

                string error = string.Empty;

                var len = 0;

                command = GetRedisReplyLine(ctoken);

                if (string.IsNullOrEmpty(command)) return null;

                if (command.IndexOf("-") == 0 && command.IndexOf(MOVED) == -1)
                {
                    responseData.Type = ResponseType.Error;
                    responseData.Data = command;
                    return responseData;
                }

                while (command == ConstHelper.ENTER)
                {
                    command = GetRedisReplyLine(ctoken);

                    if (command.IndexOf("-") == 0 && command.IndexOf(MOVED) == -1)
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = command;
                        return responseData;
                    }
                    if (string.IsNullOrEmpty(command)) return null;
                }

                var temp = Redirect<IEnumerable<IdFiled>>(command);

                if (temp != null)
                {
                    responseData = temp;
                }
                else
                {
                    if (command.IndexOf("-") == 0)
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = command;
                        return responseData;
                    }
                    responseData.Type = ResponseType.Lines;
                    responseData = GetStreamRangeData(command, ctoken);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("RedisCoder.Decoder", ex);
                responseData.Type = ResponseType.Error;
                responseData.Data = "操作超时";
            }
            return responseData;
        }
    }
}
