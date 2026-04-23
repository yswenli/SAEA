/****************************************************************************
*��Ŀ���ƣ�SAEA.DNS
*CLR �汾��3.0
*�������ƣ�WENLI-PC
*�����ռ䣺SAEA.DNS.Coder
*�� �� �ƣ�IRequestCoder
*�� �� �ţ�v5.0.0.1
*�����ˣ� yswenli
*�������䣺yswenli@outlook.com
*����ʱ�䣺2019/11/28 22:43:28
*������
*=====================================================================
*�޸�ʱ�䣺2019/11/28 22:43:28
*�� �� �ˣ� yswenli
*�汾�ţ� v7.0.0.1
*��    ����
*****************************************************************************/
using SAEA.DNS.Protocol;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Coder
{
    public interface IRequestCoder {
        Task<IResponse> Code(IRequest request, CancellationToken cancellationToken = default(CancellationToken));
    }
}
