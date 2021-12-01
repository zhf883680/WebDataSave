## 功能说明
1. /savedata/getnum?type=lock 获取一个数字,每次访问增加1 type不同数据不同
2. /savedata/resetnum?type=lock 将此type的数字重置为0
3. 每天8点30,18点发送上下班路线信息到手机
## 环境
NET6.0  
## 配置文件
请先修改配置文件  
## docker部署说明
## 发布以后
## 创建dockerfile文件
```
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app   
COPY . /app
EXPOSE 80   
ENTRYPOINT ["dotnet", "WebDataSave.dll"]
```
## build
`docker build -t webdata .`  webdata:可替换
## 运行
### 请先手动创建 webdata 挂载盘
`docker run --name=webdata -e TZ=Asia/Shanghai -v webdata:/app -d -p 8056:2050 --restart always webdata`
## 更新
将需要更新的文件复制到webdata/_data/app下  
示例:  
`cp /tmp/upload/WebDataSave.dll /opt/volumes/webdata/_data/`  
重启镜像:  
`docker restart webdata`  
