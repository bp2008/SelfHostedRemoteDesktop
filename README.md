# SelfHostedRemoteDesktop
**Incomplete.  Non-functional.  Come back later.**

Self Hosted Remote Desktop (**SHRD** for short) is an open-source remote desktop system I am developing with the goal of reducing my dependence on TeamViewer or similar third-party hosted services for remote computer access.

## Comparison with other remote-desktop tools and services

|                               | SHRD     | TeamViewer | VNC          | MeshCentral  |
| ----------------------------- | -------- | ---------- | ------------ |--------------|
| Free version available        | ✅ | ✅ | ✅ | ✅ |
| Free for commercial use       | ✅ | ❌ | ✅ | ✅ |
| Open Source                   | ✅ | ❌ | ✅ | ✅ |
| Easy host deployment for technologically-challenged users (no firewall modifications)  | ✅ | ✅ | ❌ | ✅ Must load a complex web URL |
| Remotely access many operating systems  | Windows 8+ only | ✅ Cross-Platform | ✅ Cross-Platform | ✅ Cross-Platform |
| Run your own centralized server         | ✅ Windows or Linux | ❌ | ❌ | ✅ Cross-Platform |
| Use without a centralized server (direct connections) | ❌ | ❌ | ✅ | ❌ |
| Peer-to-peer connections for minimal latency | ❌ (eventually) | ✅ | ✅ | ✅ |
| Web client                           | ✅ | ✅ | ❌ | ✅ |
| Native clients                       | ❌ | ✅ | ✅ | ❌ |
| Unlimited concurrent sessions        | ✅ | ❌ | ✅ | ✅ |
| Remote audio                         | ❌ | ✅ | ❌ | ❌ |

Of course, the above table is a little unfair because TeamViewer has had years of professional (paid) development and has amassed many features I do not care about, such as remote printing.

**Update 2020-11-19:** I've discovered [MeshCentral](https://github.com/Ylianst/MeshCentral), an impressive application that shares most of the goals of this project, but is much more ambitious (and actually usable).

## Design

SHRD has two major components:  The `Host Service` and the `Master Server`.

### Host Service
SHRD's `Host Service` is the program which runs on each computer that is to be remotely accessed.  Each `Host Service` maintains a TCP connection to the `Master Server`, which enables two-way communication through firewalls.

Initially only Windows 8 and 10 will be supported.  Most of the responsibilities of a `Host Service` involve accessing operating system APIs for desktop video capture and keyboard/mouse emulation, so it is not easy to make this cross-platform.

### Master Server
SHRD's `Master Server` is being designed to be lightweight and cross-platform using the Mono framework, so it can be run on an inexpensive cloud server or even a Raspberry Pi.  It requires only one TCP port to be open to the outside world, for HTTPS traffic (typically port 443).  A reverse-proxy server like nginx will be compatible, but not required.

The `Master Server` provides an HTML5 web client for remote access of connected computers.  Native clients are a possibility for the future, even without significant modification to the `Master Server` code.  The `Master Server` also provides an administrative web interface for user and computer management.

### System Diagram

This is a bit silly, but I built a diagram illustrating the basic architecture of Self Hosted Remote Desktop.  Essentially, SHRD's `Master Server` is intended to fill the roles of a cloud remote desktop service provider, on a much smaller scale.

![System Diagram](https://i.imgur.com/anlKuO0.png)

## Building from source

This solution is only tested with Visual Studio 2017 Community Edition.  To build, you will also need my general-purpose utility project, [BPUtil](https://github.com/bp2008/BPUtil).  Full compatibility with said utility project is not guaranteed, since I often don't check in its changes in a timely manner.
