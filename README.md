# SelfHostedRemoteDesktop
Incomplete.  Non-functional.  Come back later.

Self Hosted Remote Desktop (SHRD for short) is an open-source remote desktop system I am developing with the goal of reducing my dependence on TeamViewer or similar third-party hosted services for remote computer access.

## Comparison with other remote-desktop tools and services

|                               | SHRD     | TeamViewer | VNC          |
| ----------------------------- |:--------:|:----------:|:------------:|
| Free version available        | ✅ | ✅ | ✅ |
| Free for commercial use       | ✅ | ❌ | ✅ |
| Open Source                   | ✅ | ❌ | ✅ |
| Easy host deployment for technologically-challenged users (no firewall modifications)  | ✅ | ✅ | ❌ |
| Remotely access many operating systems  | Windows 8+ only | ✅ Cross-Platform | ✅ Cross-Platform |
| Run your own centralized server         | ✅ Windows or Linux | ❌ | ❌ |
| Use without a centralized server (direct connections) | ❌ | ❌ | ✅ |
| Peer-to-peer connections for minimal latency | ❌ | ✅ | ✅ |
| Web client                           | ✅ | ✅ | ❌ |
| Native clients                       | ❌ | ✅ | ✅ |
| Unlimited concurrent sessions        | ✅ | ❌ | ✅ |
| Remote audio                         | ❌ | ✅ | ❌ |

Of course, the above table is a little unfair because TeamViewer has had years of professional (paid) development and has amassed many features I do not care about, such as remote printing


## System Diagram

This is a bit silly, but I built a diagram illustrating the basic architecture of Self Hosted Remote Desktop.

![System Diagram](https://i.imgur.com/anlKuO0.png)
