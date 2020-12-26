//
//此代码为SAEA.MVC.APISdkCodeGenerator于2020-12-26 16:26:20.254生成，请尽量不要修改
//
function SaeaApiSdk(url) {    
    var url=url;
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

    // Ajax/Test
    this.AjaxTestGet= function (str,sucess, error) {
        request({
            url: `${url}/api/Ajax/Test`,
            type: 'Get',
            data: {
                str:str
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
    }
    // Ajax/Test
    this.AjaxTestPost= function (str,sucess, error) {
        request({
            url: `${url}/api/Ajax/Test`,
            type: 'Post',
            data: {
                str:str
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
    }
    // Ajax/Test2
    this.AjaxTest2Get= function (str,sucess, error) {
        request({
            url: `${url}/api/Ajax/Test2`,
            type: 'Get',
            data: {
                str:str
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
    }
    // Ajax/Test2
    this.AjaxTest2Post= function (str,sucess, error) {
        request({
            url: `${url}/api/Ajax/Test2`,
            type: 'Post',
            data: {
                str:str
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
    }
    // Ajax/Test3
    this.AjaxTest3Get= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Ajax/Test3`,
            type: 'Get',
            data: {
                ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Ajax/Test3
    this.AjaxTest3Post= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Ajax/Test3`,
            type: 'Post',
            data: {
                ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Asynchronous/Hello
    this.AsynchronousHelloGet= function (sucess, error) {
        request({
            url: `${url}/api/Asynchronous/Hello`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Asynchronous/Hello
    this.AsynchronousHelloPost= function (sucess, error) {
        request({
            url: `${url}/api/Asynchronous/Hello`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Asynchronous/Test
    this.AsynchronousTestGet= function (id,sucess, error) {
        request({
            url: `${url}/api/Asynchronous/Test`,
            type: 'Get',
            data: {
                id:id
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
    }
    // Asynchronous/Test
    this.AsynchronousTestPost= function (id,sucess, error) {
        request({
            url: `${url}/api/Asynchronous/Test`,
            type: 'Post',
            data: {
                id:id
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
    }
    // Error/Test1
    this.ErrorTest1Get= function (sucess, error) {
        request({
            url: `${url}/api/Error/Test1`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Error/Test1
    this.ErrorTest1Post= function (sucess, error) {
        request({
            url: `${url}/api/Error/Test1`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // File/Download
    this.FileDownloadGet= function (sucess, error) {
        request({
            url: `${url}/api/File/Download`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // File/Download
    this.FileDownloadPost= function (sucess, error) {
        request({
            url: `${url}/api/File/Download`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // File/Upload
    this.FileUploadGet= function (name,sucess, error) {
        request({
            url: `${url}/api/File/Upload`,
            type: 'Get',
            data: {
                name:name
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
    }
    // File/Upload
    this.FileUploadPost= function (name,sucess, error) {
        request({
            url: `${url}/api/File/Upload`,
            type: 'Post',
            data: {
                name:name
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
    }
    // Home/Get
    this.HomeGetGet= function (id,sucess, error) {
        request({
            url: `${url}/api/Home/Get`,
            type: 'Get',
            data: {
                id:id
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
    }
    // Home/Get
    this.HomeGetPost= function (id,sucess, error) {
        request({
            url: `${url}/api/Home/Get`,
            type: 'Post',
            data: {
                id:id
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
    }
    // Home/Show
    this.HomeShowGet= function (sucess, error) {
        request({
            url: `${url}/api/Home/Show`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Home/Show
    this.HomeShowPost= function (sucess, error) {
        request({
            url: `${url}/api/Home/Show`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Home/Update
    this.HomeUpdateGet= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Home/Update`,
            type: 'Get',
            data: {
                isFemale:isFemale,ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Home/Update
    this.HomeUpdatePost= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Home/Update`,
            type: 'Post',
            data: {
                isFemale:isFemale,ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Home/Set
    this.HomeSetGet= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Home/Set`,
            type: 'Get',
            data: {
                isFemale:isFemale,ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Home/Set
    this.HomeSetPost= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Home/Set`,
            type: 'Post',
            data: {
                isFemale:isFemale,ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Home/GetModels
    this.HomeGetModelsGet= function (version,UID,Token,PageIndex,PageSize,sucess, error) {
        request({
            url: `${url}/api/Home/GetModels`,
            type: 'Get',
            data: {
                version:version,UID:UID,Token:Token,PageIndex:PageIndex,PageSize:PageSize
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
    }
    // Home/GetModels
    this.HomeGetModelsPost= function (version,UID,Token,PageIndex,PageSize,sucess, error) {
        request({
            url: `${url}/api/Home/GetModels`,
            type: 'Post',
            data: {
                version:version,UID:UID,Token:Token,PageIndex:PageIndex,PageSize:PageSize
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
    }
    // Home/Test
    this.HomeTestGet= function (sucess, error) {
        request({
            url: `${url}/api/Home/Test`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Home/Test
    this.HomeTestPost= function (sucess, error) {
        request({
            url: `${url}/api/Home/Test`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // SaeaSdkTest/GetList
    this.SaeaSdkTestGetListGet= function (sucess, error) {
        request({
            url: `${url}/api/SaeaSdkTest/GetList`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // SaeaSdkTest/GetList
    this.SaeaSdkTestGetListPost= function (sucess, error) {
        request({
            url: `${url}/api/SaeaSdkTest/GetList`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Test/Get
    this.TestGetGet= function (sucess, error) {
        request({
            url: `${url}/api/Test/Get`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Test/Get
    this.TestGetPost= function (sucess, error) {
        request({
            url: `${url}/api/Test/Get`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Test/Other
    this.TestOtherPost= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Test/Other`,
            type: 'Post',
            data: {
                ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Test/Other
    this.TestOtherGet= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `${url}/api/Test/Other`,
            type: 'Get',
            data: {
                ID:ID,UserName:UserName,NickName:NickName
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
    }
    // Test/Timeout
    this.TestTimeoutGet= function (sucess, error) {
        request({
            url: `${url}/api/Test/Timeout`,
            type: 'Get',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }
    // Test/Timeout
    this.TestTimeoutPost= function (sucess, error) {
        request({
            url: `${url}/api/Test/Timeout`,
            type: 'Post',
            timeout: 3000,
            success: function (data) {
                sucess(data);
            },
            //异常处理
            error: function (e) {
                error(e);
            }
        });
    }

}