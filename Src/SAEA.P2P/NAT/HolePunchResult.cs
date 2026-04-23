/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.P2P.NAT
*文件名： HolePunchResult
*版本号： v26.4.23.1
*唯一标识：58cf4902-46ec-49e8-8a0d-40cfa31e8c54
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:07:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:07:31
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
namespace SAEA.P2P.NAT
{
    public class HolePunchResult
    {
        public bool Success { get; set; }
        public NATType SourceNATType { get; set; }
        public NATType TargetNATType { get; set; }
        public System.Net.IPEndPoint EstablishedAddress { get; set; }
        public string ErrorMessage { get; set; }
        public int Attempts { get; set; }
        
        public static HolePunchResult Succeeded(System.Net.IPEndPoint address, NATType sourceNat, NATType targetNat, int attempts)
        {
            return new HolePunchResult
            {
                Success = true,
                EstablishedAddress = address,
                SourceNATType = sourceNat,
                TargetNATType = targetNat,
                Attempts = attempts
            };
        }
        
        public static HolePunchResult Failed(string error, NATType sourceNat, NATType targetNat)
        {
            return new HolePunchResult
            {
                Success = false,
                ErrorMessage = error,
                SourceNATType = sourceNat,
                TargetNATType = targetNat
            };
        }
    }
}