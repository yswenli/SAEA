//
//此代码为SAEA.MVC.APISdkCodeGenerator于2020-12-25 17:23:34.273生成，请尽量不要修改
//
function SaeaApiSdk() {
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
    this.AjaxTest= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test`,
            type: 'get',
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
    this.AjaxTest= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test`,
            type: 'post',
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
    this.AjaxTest2= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test2`,
            type: 'get',
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
    this.AjaxTest2= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test2`,
            type: 'post',
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
    this.AjaxTest3= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Ajax/Test3`,
            type: 'get',
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
    this.AjaxTest3= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Ajax/Test3`,
            type: 'post',
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
    // Error/Test1
    this.ErrorTest1= function (sucess, error) {
        request({
            url: `/api/Error/Test1`,
            type: 'get',
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
    this.ErrorTest1= function (sucess, error) {
        request({
            url: `/api/Error/Test1`,
            type: 'post',
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
    this.FileDownload= function (sucess, error) {
        request({
            url: `/api/File/Download`,
            type: 'get',
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
    this.FileDownload= function (sucess, error) {
        request({
            url: `/api/File/Download`,
            type: 'post',
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
    this.FileUpload= function (name,sucess, error) {
        request({
            url: `/api/File/Upload`,
            type: 'post',
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
    this.HomeGet= function (id,sucess, error) {
        request({
            url: `/api/Home/Get`,
            type: 'post',
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
    this.HomeGet= function (id,sucess, error) {
        request({
            url: `/api/Home/Get`,
            type: 'get',
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
    this.HomeShow= function (sucess, error) {
        request({
            url: `/api/Home/Show`,
            type: 'get',
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
    this.HomeShow= function (sucess, error) {
        request({
            url: `/api/Home/Show`,
            type: 'post',
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
    this.HomeUpdate= function (id,sucess, error) {
        request({
            url: `/api/Home/Update`,
            type: 'get',
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
    // Home/Update
    this.HomeUpdate= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Home/Update`,
            type: 'post',
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
    this.HomeSet= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Home/Set`,
            type: 'get',
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
    this.HomeSet= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Home/Set`,
            type: 'post',
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
    this.HomeGetModels= function (version,UID,Token,PageIndex,PageSize,sucess, error) {
        request({
            url: `/api/Home/GetModels`,
            type: 'get',
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
    this.HomeGetModels= function (version,UID,Token,PageIndex,PageSize,sucess, error) {
        request({
            url: `/api/Home/GetModels`,
            type: 'post',
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
    this.HomeTest= function (sucess, error) {
        request({
            url: `/api/Home/Test`,
            type: 'post',
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
    this.SaeaSdkTestGetList= function (sucess, error) {
        request({
            url: `/api/SaeaSdkTest/GetList`,
            type: 'get',
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
    this.SaeaSdkTestGetList= function (sucess, error) {
        request({
            url: `/api/SaeaSdkTest/GetList`,
            type: 'post',
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
    this.TestGet= function (sucess, error) {
        request({
            url: `/api/Test/Get`,
            type: 'get',
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
    this.TestGet= function (sucess, error) {
        request({
            url: `/api/Test/Get`,
            type: 'post',
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
    this.TestOther= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Test/Other`,
            type: 'post',
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
    this.TestOther= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Test/Other`,
            type: 'get',
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
    this.TestTimeout= function (sucess, error) {
        request({
            url: `/api/Test/Timeout`,
            type: 'get',
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
    this.TestTimeout= function (sucess, error) {
        request({
            url: `/api/Test/Timeout`,
            type: 'post',
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
    this.AjaxTest= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test`,
            type: 'get',
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
    this.AjaxTest= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test`,
            type: 'post',
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
    this.AjaxTest2= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test2`,
            type: 'get',
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
    this.AjaxTest2= function (str,sucess, error) {
        request({
            url: `/api/Ajax/Test2`,
            type: 'post',
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
    this.AjaxTest3= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Ajax/Test3`,
            type: 'get',
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
    this.AjaxTest3= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Ajax/Test3`,
            type: 'post',
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
    // Error/Test1
    this.ErrorTest1= function (sucess, error) {
        request({
            url: `/api/Error/Test1`,
            type: 'get',
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
    this.ErrorTest1= function (sucess, error) {
        request({
            url: `/api/Error/Test1`,
            type: 'post',
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
    this.FileDownload= function (sucess, error) {
        request({
            url: `/api/File/Download`,
            type: 'get',
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
    this.FileDownload= function (sucess, error) {
        request({
            url: `/api/File/Download`,
            type: 'post',
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
    this.FileUpload= function (name,sucess, error) {
        request({
            url: `/api/File/Upload`,
            type: 'post',
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
    this.HomeGet= function (id,sucess, error) {
        request({
            url: `/api/Home/Get`,
            type: 'post',
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
    this.HomeGet= function (id,sucess, error) {
        request({
            url: `/api/Home/Get`,
            type: 'get',
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
    this.HomeShow= function (sucess, error) {
        request({
            url: `/api/Home/Show`,
            type: 'get',
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
    this.HomeShow= function (sucess, error) {
        request({
            url: `/api/Home/Show`,
            type: 'post',
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
    this.HomeUpdate= function (id,sucess, error) {
        request({
            url: `/api/Home/Update`,
            type: 'get',
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
    // Home/Update
    this.HomeUpdate= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Home/Update`,
            type: 'post',
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
    this.HomeSet= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Home/Set`,
            type: 'get',
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
    this.HomeSet= function (isFemale,ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Home/Set`,
            type: 'post',
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
    this.HomeGetModels= function (version,UID,Token,PageIndex,PageSize,sucess, error) {
        request({
            url: `/api/Home/GetModels`,
            type: 'get',
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
    this.HomeGetModels= function (version,UID,Token,PageIndex,PageSize,sucess, error) {
        request({
            url: `/api/Home/GetModels`,
            type: 'post',
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
    this.HomeTest= function (sucess, error) {
        request({
            url: `/api/Home/Test`,
            type: 'post',
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
    this.SaeaSdkTestGetList= function (sucess, error) {
        request({
            url: `/api/SaeaSdkTest/GetList`,
            type: 'get',
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
    this.SaeaSdkTestGetList= function (sucess, error) {
        request({
            url: `/api/SaeaSdkTest/GetList`,
            type: 'post',
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
    this.TestGet= function (sucess, error) {
        request({
            url: `/api/Test/Get`,
            type: 'get',
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
    this.TestGet= function (sucess, error) {
        request({
            url: `/api/Test/Get`,
            type: 'post',
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
    this.TestOther= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Test/Other`,
            type: 'post',
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
    this.TestOther= function (ID,UserName,NickName,sucess, error) {
        request({
            url: `/api/Test/Other`,
            type: 'get',
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
    this.TestTimeout= function (sucess, error) {
        request({
            url: `/api/Test/Timeout`,
            type: 'get',
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
    this.TestTimeout= function (sucess, error) {
        request({
            url: `/api/Test/Timeout`,
            type: 'post',
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