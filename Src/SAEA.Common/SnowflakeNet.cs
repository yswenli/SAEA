/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：SnowflakeNet
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/9/30 14:13:48
*描述：
*=====================================================================
*修改时间：2020/9/30 14:13:48
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Common
{
    /// <summary>
    /// 雪花算法,
    /// 0 - 0000000000 0000000000 0000000000 0000000000 0 - 00000 - 00000 - 000000000000
    /// 第一位为未使用，接下来的41位为毫秒级时间，然后是5位datacenterId和5位workerId(10位的长度最多支持部署1024个节点）
    /// 最后12位是毫秒内的计数（12位的计数顺序号支持每个节点每毫秒产生4096个ID序号）
    /// 一共加起来刚好64位，为一个Long型。(转换成字符串长度为18)
    /// </summary>
    public class SnowflakeNet
    {
        //基准时间
        private static long _startStmp = 1288834974657L;
        /*每一部分占用的位数*/
        //机器标识位数
        const int MachineIdBits = 5;
        //数据标志位数
        const int DatacenterIdBits = 5;
        //序列号识位数
        const int SequenceBits = 12;

        /* 每一部分的最大值*/
        //机器ID最大值
        const long MaxMachineNum = -1L ^ (-1L << MachineIdBits);
        //数据标志ID最大值
        const long MaxDatacenterNum = -1L ^ (-1L << DatacenterIdBits);
        //序列号ID最大值
        private const long _maxSequenceNum = -1L ^ (-1L << SequenceBits);

        /*每一部分向左的位移*/

        //机器ID偏左移12位
        private const int MachineShift = SequenceBits;
        //数据ID偏左移17位
        private const int DatacenterIdShift = SequenceBits + MachineIdBits;
        //时间毫秒左移22位
        private const int TimestampLeftShift = SequenceBits + MachineIdBits + DatacenterIdBits;


        private long _sequence = 0L;//序列号
        private long _lastTimestamp = -1L;//上一次时间戳

        /// <summary>
        /// 机器标识
        /// </summary>
        public long MachineId { get; protected set; }//机器标识
        /// <summary>
        /// 数据中心
        /// </summary>
        public long DatacenterId { get; protected set; }//数据中心


        private readonly object _lock = new Object();

        /// <summary>
        /// 雪花算法
        /// </summary>
        /// <param name="machineId"></param>
        /// <param name="datacenterId"></param>
        public SnowflakeNet(long machineId, long datacenterId)
        {
            // 如果超出范围就抛出异常
            if (machineId > MaxMachineNum || machineId < 0)
            {
                throw new ArgumentException(string.Format("machineId 必须大于0，MaxMachineNum： {0}", MaxMachineNum));
            }

            if (datacenterId > MaxDatacenterNum || datacenterId < 0)
            {
                throw new ArgumentException(string.Format("datacenterId必须大于0，且不能大于MaxDatacenterNum： {0}", MaxDatacenterNum));
            }

            //先检验再赋值
            MachineId = machineId;
            DatacenterId = datacenterId;
        }

        /// <summary>
        /// 下一个id
        /// </summary>
        /// <returns></returns>
        public long NextId()
        {
            lock (_lock)
            {
                var timestamp = TimeGen();
                if (timestamp < _lastTimestamp)
                {
                    throw new Exception(string.Format("时间戳必须大于上一次生成ID的时间戳.  拒绝为{0}毫秒生成id", _lastTimestamp - timestamp));
                }

                //如果上次生成时间和当前时间相同,在同一毫秒内
                if (_lastTimestamp == timestamp)
                {
                    //sequence自增，和sequenceMask相与一下，去掉高位
                    _sequence = (_sequence + 1) & _maxSequenceNum;
                    //判断是否溢出,也就是每毫秒内超过1024，当为1024时，与sequenceMask相与，sequence就等于0
                    if (_sequence == 0L)
                    {
                        //等待到下一毫秒
                        timestamp = TilNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    //如果和上次生成时间不同,重置sequence，就是下一毫秒开始，sequence计数重新从0开始累加,
                    //为了保证尾数随机性更大一些,最后一位可以设置一个随机数
                    _sequence = 0L;//new Random().Next(10);
                }

                _lastTimestamp = timestamp;
                return ((timestamp - _startStmp) << TimestampLeftShift) | (DatacenterId << DatacenterIdShift) | (MachineId << MachineShift) | _sequence;
            }
        }

        /// <summary>
        /// 防止产生的时间比之前的时间还要小（由于NTP回拨等问题）,保持增量的趋势
        /// </summary>
        /// <param name="lastTimestamp"></param>
        /// <returns></returns>
        protected virtual long TilNextMillis(long lastTimestamp)
        {
            var timestamp = TimeGen();
            while (timestamp <= lastTimestamp)
            {
                timestamp = TimeGen();
            }
            return timestamp;
        }

        /// <summary>
        /// 获取当前的时间戳
        /// </summary>
        /// <returns></returns>
        protected virtual long TimeGen()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }

    /// <summary>
    /// id生成器，
    /// 简易使用雪花算法
    /// </summary>
    public static class IdWorker
    {
        static SnowflakeNet _snowflakeNet;

        /// <summary>
        /// id生成器
        /// </summary>
        static IdWorker()
        {
            _snowflakeNet = new SnowflakeNet(1, 1);
        }

        /// <summary>
        /// 获取id
        /// </summary>
        /// <returns></returns>
        public static long GetId() => _snowflakeNet.NextId();
    }
}
