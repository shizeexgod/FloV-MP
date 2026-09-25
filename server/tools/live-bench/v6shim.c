// Прослойка для облачного теста: в контейнере нет IPv6, а alt:V открывает
// сокет AF_INET6. Подменяем на AF_INET и переводим адреса ::ffff:a.b.c.d <-> a.b.c.d.
#define _GNU_SOURCE
#include <dlfcn.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <string.h>
#include <errno.h>
#include <stdint.h>
static char emu[65536];
#define R(name, ret, ...) static ret (*real_##name)(__VA_ARGS__); if(!real_##name) real_##name = dlsym(RTLD_NEXT, #name)
static int to4(const struct sockaddr* a, socklen_t l, struct sockaddr_in* o){
  if(!a || a->sa_family!=AF_INET6 || l < sizeof(struct sockaddr_in6)) return 0;
  const struct sockaddr_in6* s=(const void*)a; memset(o,0,sizeof *o); o->sin_family=AF_INET; o->sin_port=s->sin6_port;
  const uint8_t* b=s->sin6_addr.s6_addr; static const uint8_t z[16]={0};
  if(!memcmp(b,z,16)) o->sin_addr.s_addr=INADDR_ANY;
  else if(!memcmp(b,z,15) && b[15]==1) o->sin_addr.s_addr=htonl(INADDR_LOOPBACK);
  else memcpy(&o->sin_addr, b+12, 4);
  return 1;
}
static void to6(struct sockaddr* a, socklen_t* l, socklen_t cap){
  if(!a || !l || a->sa_family!=AF_INET || cap < sizeof(struct sockaddr_in6)) return;
  struct sockaddr_in s; memcpy(&s,a,sizeof s); struct sockaddr_in6 o; memset(&o,0,sizeof o);
  o.sin6_family=AF_INET6; o.sin6_port=s.sin_port; o.sin6_addr.s6_addr[10]=0xff; o.sin6_addr.s6_addr[11]=0xff;
  memcpy(o.sin6_addr.s6_addr+12,&s.sin_addr,4); memcpy(a,&o,sizeof o); *l=sizeof o;
}
int socket(int d,int t,int p){ R(socket,int,int,int,int); if(d==AF_INET6){ int fd=real_socket(AF_INET,t,p); if(fd>=0&&fd<65536) emu[fd]=1; return fd;} return real_socket(d,t,p); }
int bind(int fd,const struct sockaddr* a,socklen_t l){ R(bind,int,int,const struct sockaddr*,socklen_t); struct sockaddr_in o; if(fd<65536&&emu[fd]&&to4(a,l,&o)) return real_bind(fd,(void*)&o,sizeof o); return real_bind(fd,a,l); }
int connect(int fd,const struct sockaddr* a,socklen_t l){ R(connect,int,int,const struct sockaddr*,socklen_t); struct sockaddr_in o; if(fd<65536&&emu[fd]&&to4(a,l,&o)) return real_connect(fd,(void*)&o,sizeof o); return real_connect(fd,a,l); }
ssize_t sendto(int fd,const void* b,size_t n,int f,const struct sockaddr* a,socklen_t l){ R(sendto,ssize_t,int,const void*,size_t,int,const struct sockaddr*,socklen_t); struct sockaddr_in o; if(fd<65536&&emu[fd]&&to4(a,l,&o)) return real_sendto(fd,b,n,f,(void*)&o,sizeof o); return real_sendto(fd,b,n,f,a,l); }
ssize_t recvfrom(int fd,void* b,size_t n,int f,struct sockaddr* a,socklen_t* l){ R(recvfrom,ssize_t,int,void*,size_t,int,struct sockaddr*,socklen_t*); socklen_t cap=l?*l:0; ssize_t r=real_recvfrom(fd,b,n,f,a,l); if(r>=0&&fd<65536&&emu[fd]) to6(a,l,cap); return r; }
ssize_t sendmsg(int fd,const struct msghdr* m,int f){ R(sendmsg,ssize_t,int,const struct msghdr*,int); if(fd<65536&&emu[fd]&&m&&m->msg_name){ struct sockaddr_in o; struct msghdr c=*m; if(to4(m->msg_name,m->msg_namelen,&o)){ c.msg_name=&o; c.msg_namelen=sizeof o; c.msg_control=NULL; c.msg_controllen=0; return real_sendmsg(fd,&c,f);} } return real_sendmsg(fd,m,f); }
ssize_t recvmsg(int fd,struct msghdr* m,int f){ R(recvmsg,ssize_t,int,struct msghdr*,int); socklen_t cap=m?m->msg_namelen:0; ssize_t r=real_recvmsg(fd,m,f); if(r>=0&&fd<65536&&emu[fd]&&m) to6(m->msg_name,&m->msg_namelen,cap); return r; }
int getsockname(int fd,struct sockaddr* a,socklen_t* l){ R(getsockname,int,int,struct sockaddr*,socklen_t*); socklen_t cap=l?*l:0; int r=real_getsockname(fd,a,l); if(!r&&fd<65536&&emu[fd]) to6(a,l,cap); return r; }
int getpeername(int fd,struct sockaddr* a,socklen_t* l){ R(getpeername,int,int,struct sockaddr*,socklen_t*); socklen_t cap=l?*l:0; int r=real_getpeername(fd,a,l); if(!r&&fd<65536&&emu[fd]) to6(a,l,cap); return r; }
int accept(int fd,struct sockaddr* a,socklen_t* l){ R(accept,int,int,struct sockaddr*,socklen_t*); socklen_t cap=l?*l:0; int r=real_accept(fd,a,l); if(r>=0&&fd<65536&&emu[fd]){ if(r<65536) emu[r]=1; to6(a,l,cap);} return r; }
int accept4(int fd,struct sockaddr* a,socklen_t* l,int fl){ R(accept4,int,int,struct sockaddr*,socklen_t*,int); socklen_t cap=l?*l:0; int r=real_accept4(fd,a,l,fl); if(r>=0&&fd<65536&&emu[fd]){ if(r<65536) emu[r]=1; to6(a,l,cap);} return r; }
int setsockopt(int fd,int lv,int nm,const void* v,socklen_t l){ R(setsockopt,int,int,int,int,const void*,socklen_t); if(fd<65536&&emu[fd]&&lv==IPPROTO_IPV6) return 0; return real_setsockopt(fd,lv,nm,v,l); }
int close(int fd){ R(close,int,int); if(fd>=0&&fd<65536) emu[fd]=0; return real_close(fd); }
