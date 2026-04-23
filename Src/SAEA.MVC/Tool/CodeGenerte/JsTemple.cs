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
*命名空间：SAEA.MVC.Tool.CodeGenerte
*文件名： JsTemple
*版本号： v26.4.23.1
*唯一标识：4b3dcd08-cf1c-4aed-8a7c-1ca4d55ddfef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/25 17:36:47
*描述：JsTemple模板类
*
*=====================================================================
*修改标记
*修改时间：2020/12/25 17:36:47
*修改人： yswenli
*版本号： v26.4.23.1
*描述：JsTemple模板类
*
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
