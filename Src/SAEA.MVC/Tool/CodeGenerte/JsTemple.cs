/****************************************************************************
*项目名称：SAEA.MVC.Tool.CodeGenerte
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC.Tool.CodeGenerte
*类 名 称：JsTemple
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/24 16:50:13
*描述：
*=====================================================================
*修改时间：2020/12/24 16:50:13
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

namespace SAEA.MVC.Tool.CodeGenerte
{
    public static class JsTemple
    {
        public const string TEMPLE = @"//
//此代码为SAEA.MVC.APISdkCodeGenerator于[[DateTime]]生成，请尽量不要修改
//
function SaeaApiSdk(url) {    
    this.url=url;
    var request = function (opt) {
        opt = opt || {};
        opt.method = !!opt.method === false ? 'POST' : opt.method.toUpperCase();
        opt.url = opt.url || '';
        opt.async = opt.async || true;
        opt.data = opt.data || null;
        opt.headers = opt.headers || null;
        opt.success = opt.success || function () { };
        var xhr = XMLHttpRequest ? new XMLHttpRequest() : new ActiveXObject('Microsoft.xhr');
        xhr.timeout = opt.timeout || 3000;
        var params = [];
        for (var k in opt.data) {
            params.push(encodeURIComponent(k) + '=' + encodeURIComponent(opt.data[k]));
        }
        var postData = params.join('&');
        if (opt.method.toUpperCase() === 'POST') {
            xhr.open(opt.method, opt.url, opt.async);
            xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded;charset=utf-8');
            for (var h in opt.headers) {
                xhr.setRequestHeader(h, opt.headers[h]);
            }
            xhr.send(postData);
        } else if (opt.method.toUpperCase() === 'GET') {
            xhr.open(opt.method, opt.url + '?' + postData, opt.async);
            for (var h in opt.headers) {
                xhr.setRequestHeader(h, opt.headers[h]);
            }
            xhr.send(null);
        }
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                opt.success(xhr.responseText);
            }
        }
    }
    this.request = request;

[[Method]]
}";

        public const string MethodTemple1 = @"    // [[Controller]]/[[Action]]
    this.[[Controller]][[Action]][[Type]]= function (sucess, error) {
        request({
            url: `${url}/api/[[Controller]]/[[Action]]`,
            type: '[[Type]]',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }";
        public const string MethodTemple2 = @"    // [[Controller]]/[[Action]]
    this.[[Controller]][[Action]][[Type]]= function ([[Inputs1]]sucess, error) {
        request({
            url: `${url}/api/[[Controller]]/[[Action]]`,
            type: '[[Type]]',
            data: {
                [[Inputs2]]
            },
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }";
        public const string MethodTemple3 = @"    // [[Controller]]/[[Action]]
    this.[[Controller]][[Action]][[Type]]= function ([[Header1]],[[Inputs1]]sucess, error) {
        request({
            url: `${url}/api/[[Controller]]/[[Action]]`,
            type: '[[Type]]',
            headers:{
                [[Header2]]
            },
            data: {
                [[Inputs2]]
            },
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }";



    }
}
