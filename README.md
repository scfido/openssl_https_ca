# OpenSSL 自签名证书制作和使用流程

## 准备工作

后续所有操作是在`ca`目录下进行。

```
openssl rand -out .rand 1024
mkdir -p ./certs
mkdir -p ./crl
mkdir -p ./db
mkdir -p ./new_certs 
touch ./db/index.txt 
touch ./db/serial.txt
echo 01 > ./db/serial.txt
```

## 制作根证书

```sh
mkdir root

# 生成ca根证书私钥
openssl genrsa -des3 -out root/root.key 4096

# 生成ca根证书请求文件
openssl req -new -key root/root.key -out root/root.csr -config openssl_root.cnf

# 自己作为ca机构签发根证书（自签发证书）
openssl ca -selfsign \
    -config openssl_root.cnf \
    -in root/root.csr \
    -extensions v3_ca \
    -days 7300 \
    -out root/root.crt
```

## 制作中间证书

```sh
mkdir mid

# 生成中间证书私钥
openssl genrsa -des3 -out mid/mid.key 4096

openssl req -new \
    -config openssl_mid.cnf \
    -sha256 \
    -key mid/mid.key \
    -out mid/mid.csr

# 检查生成的 csr
openssl req -text -noout -in mid/mid.csr

# 生成中间证书
openssl ca -config openssl_root.cnf \
    -extensions v3_intermediate_ca \
    -days 3650 -notext -md sha256 \
    -in mid/mid.csr \
    -out mid/mid.crt
```

## 创建证书链文件

当 web 浏览器等应用程序试图验证中间 CA 颁发的证书时，它还必须根据根证书验证中间证书。这就需要构建完整的证书信任链供应用程序验证。所谓的证书链，简单的说就是把根证书和中间证书按照顺序放置在同一个证书文件中。重点是：`中间证书在上面，根证书在下面`。比如为我们的中间证书创建证书链：

```sh
cat mid/mid.crt root/root.crt > mid/mid-chain.crt
```

在 windows 系统中一般使用 p12 格式，所以我们还需要创建一个 p12 格式的证书链：

```sh
openssl pkcs12 -export \
    -name "mid chain" \
    -inkey mid/mid.key \
    -in mid/mid.crt \
    -certfile mid/mid-chain.crt \
    -out mid/mid-chain.p12
```

## 创建服务器证书

```sh
mkdir server

# 生成服务器证书私钥
openssl genrsa -out server/server.key 2048
```
> 注意，这里我们没有使用 `-aes256` 选项，这样创建的秘钥不包含密码。如果要创建 web 服务器用的 `ssl` 证书，一定不要为秘钥设置密码！否则在每次重启 web 服务的时候都需要输入密码！

在`openssl_server.cnf`文件中修改证书对应的域名和IP。

```ini
[ alt_names ]
DNS.1 = www.test.com
DNS.2 = test.com
IP.1 = 192.168.0.96
```



```sh
# 生成 csr：

openssl req -config openssl_server.cnf \
    -key server/server.key \
    -new -sha256 \
    -out server/server.csr

# 生成服务器证书
openssl ca -config openssl_mid.cnf \
    -extensions server_cert -days 1000 -notext -md sha256 \
    -in server/server.csr \
    -out server/server.crt

# 导出pfx格式的证书
openssl pkcs12 -export -out server/server.pfx -inkey server/server.key -in server/server.crt
```
> 如果发生 "TXT_DB error number 2" 的错误，把 db/index.txt 文件中相同名称的记录删除即可。这个文件是 OpenSSL CA 工具存储数据的数据库

## 证书使用

### 服务端

asp.net 5网站使用证书示例.

```c#
public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var x509ca = new X509Certificate2(File.ReadAllBytes("server.pfx"), "123456");
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseKestrel(option =>
                    {
                        option.ListenAnyIP(5002, config => config.UseHttps(x509ca));
                    });
                })
            ;
    }
```

### 客户端

Windows系统导入中间证书，浏览器访问网站才显示为信任证书。

```powershell
 Import-Certificate -FilePath mid/mid.crt  -CertStoreLocation 'Cert:\LocalMachine\Root'
```

