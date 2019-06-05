#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [BuildArch] [LinuxCodeName] [lldbx.y] [--skipunmount] --rootfsdir <directory>]"
    echo "BuildArch can be: arm(default), armel, arm64, x86"
    echo "LinuxCodeName - optional, Code name for Linux, can be: trusty, xenial(default), zesty, stretch, buster, bionic, alpine, centos7. If BuildArch is armel, LinuxCodeName is jessie(default) or tizen."
    echo "lldbx.y - optional, LLDB version, can be: lldb3.9(default), lldb4.0, lldb5.0, lldb6.0 no-lldb. Ignored for alpine"
    echo "--skipunmount - optional, will skip the unmount of rootfs folder."
    exit 1
}

__LinuxCodeName=xenial
__CrossDir=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
__InitialDir=$PWD
__BuildArch=arm
__UbuntuArch=armhf
__UbuntuRepo="http://ports.ubuntu.com/"
__LLDB_Package="liblldb-3.9-dev"
__SkipUnmount=0

# base development support
__UbuntuPackages="build-essential"

__AlpinePackages="alpine-base"
__AlpinePackages+=" build-base"
__AlpinePackages+=" linux-headers"
__AlpinePackages+=" lldb-dev"
__AlpinePackages+=" llvm-dev"

__AlpinePackages+=" llvm-dev"

# symlinks fixer
__UbuntuPackages+=" symlinks"

__CentosPackages+=" gcc"

# CoreCLR and CoreFX dependencies
__UbuntuPackages+=" libicu-dev"
__UbuntuPackages+=" liblttng-ust-dev"
__UbuntuPackages+=" libunwind8-dev"

__AlpinePackages+=" gettext-dev"
__AlpinePackages+=" icu-dev"
__AlpinePackages+=" libunwind-dev"
__AlpinePackages+=" lttng-ust-dev"

__CentosPackages+=" gettext-devel"
__CentosPackages+=" libicu-devel"
__CentosPackages+=" libunwind-devel"

# CoreFX dependencies
__UbuntuPackages+=" libcurl4-openssl-dev"
__UbuntuPackages+=" libkrb5-dev"
__UbuntuPackages+=" libssl-dev"
__UbuntuPackages+=" zlib1g-dev"

__AlpinePackages+=" curl-dev"
__AlpinePackages+=" krb5-dev"
__AlpinePackages+=" openssl-dev"
__AlpinePackages+=" zlib-dev"

__CentosPackages+=" curl-devel"
__CentosPackages+=" krb5-devel"
__CentosPackages+=" openssl-devel"
__CentosPackages+=" zlib-devel "

__UnprocessedBuildArgs=
while :; do
    if [ $# -le 0 ]; then
        break
    fi

    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -?|-h|--help)
            usage
            exit 1
            ;;
        arm)
            __BuildArch=arm
            __UbuntuArch=armhf
            __AlpineArch=armhf
            __QEMUArch=arm
            ;;
        arm64)
            __BuildArch=arm64
            __UbuntuArch=arm64
            __AlpineArch=aarch64
            __CentosArch=aarch64
            __QEMUArch=aarch64
            ;;
        armel)
            __BuildArch=armel
            __UbuntuArch=armel
            __UbuntuRepo="http://ftp.debian.org/debian/"
            __LinuxCodeName=jessie
            ;;
        x86)
            __BuildArch=x86
            __UbuntuArch=i386
            __UbuntuRepo="http://archive.ubuntu.com/ubuntu/"
            ;;
        lldb3.6)
            __LLDB_Package="lldb-3.6-dev"
            ;;
        lldb3.8)
            __LLDB_Package="lldb-3.8-dev"
            ;;
        lldb3.9)
            __LLDB_Package="liblldb-3.9-dev"
            ;;
        lldb4.0)
            __LLDB_Package="liblldb-4.0-dev"
            ;;
        lldb5.0)
            __LLDB_Package="liblldb-5.0-dev"
            ;;
        lldb6.0)
            __LLDB_Package="liblldb-6.0-dev"
            ;;
        no-lldb)
            unset __LLDB_Package
            ;;
        trusty) # Ubuntu 14.04
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=trusty
            fi
            ;;
        xenial) # Ubuntu 16.04
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=xenial
            fi
            ;;
        zesty) # Ubuntu 17.04
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=zesty
            fi
            ;;
        bionic) # Ubuntu 18.04
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=bionic
            fi
            ;;
        jessie) # Debian 8
            __LinuxCodeName=jessie
            __UbuntuRepo="http://ftp.debian.org/debian/"
            ;;
        stretch) # Debian 9
            __LinuxCodeName=stretch
            __UbuntuRepo="http://ftp.debian.org/debian/"
            __LLDB_Package="liblldb-6.0-dev"
            ;;
        buster) # Debian 10
            __LinuxCodeName=buster
            __UbuntuRepo="http://ftp.debian.org/debian/"
            __LLDB_Package="liblldb-6.0-dev"
            ;;
        tizen)
            if [ "$__BuildArch" != "armel" ]; then
                echo "Tizen is available only for armel."
                usage;
                exit 1;
            fi
            __LinuxCodeName=
            __UbuntuRepo=
            __Tizen=tizen
            ;;
        alpine)
            __LinuxCodeName=alpine
            __UbuntuRepo=
            ;;
        centos7)
            __LinuxCodeName=centos7
            __UbuntuRepo=
            ;;
        --skipunmount)
            __SkipUnmount=1
            ;;
        --rootfsdir|-rootfsdir)
            shift
            __RootfsDir=$1
            ;;
        *)
            __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
            ;;
    esac

    shift
done

if [ "$__BuildArch" == "armel" ]; then
    __LLDB_Package="lldb-3.5-dev"
fi
__UbuntuPackages+=" ${__LLDB_Package:-}"

if [ -z "$__RootfsDir" ] && [ ! -z "$ROOTFS_DIR" ]; then
    __RootfsDir=$ROOTFS_DIR
fi

if [ -z "$__RootfsDir" ]; then
    __RootfsDir="$__CrossDir/../../../.tools/rootfs/$__BuildArch"
fi

if [ -d "$__RootfsDir" ]; then
    if [ $__SkipUnmount == 0 ]; then
        umount $__RootfsDir/*
    fi
    rm -rf $__RootfsDir
fi

if [[ "$__LinuxCodeName" == "alpine" ]]; then
    __ApkToolsVersion=2.9.1
    __AlpineVersion=3.7
    __ApkToolsDir=$(mktemp -d)
    wget https://github.com/alpinelinux/apk-tools/releases/download/v$__ApkToolsVersion/apk-tools-$__ApkToolsVersion-x86_64-linux.tar.gz -P $__ApkToolsDir
    tar -xf $__ApkToolsDir/apk-tools-$__ApkToolsVersion-x86_64-linux.tar.gz -C $__ApkToolsDir
    mkdir -p $__RootfsDir/usr/bin
    cp -v /usr/bin/qemu-$__QEMUArch-static $__RootfsDir/usr/bin
    $__ApkToolsDir/apk-tools-$__ApkToolsVersion/apk \
      -X http://dl-cdn.alpinelinux.org/alpine/v$__AlpineVersion/main \
      -X http://dl-cdn.alpinelinux.org/alpine/v$__AlpineVersion/community \
      -X http://dl-cdn.alpinelinux.org/alpine/edge/testing \
      -U --allow-untrusted --root $__RootfsDir --arch $__AlpineArch --initdb \
      add $__AlpinePackages
    rm -r $__ApkToolsDir
elif [[ "$__LinuxCodeName" == "centos7" ]]; then
    RPM=`which rpm`
    if [ -z "$RPM" ]; then
        echo "Please install 'rpm' executable."
        exit 1;
    fi

    if [ -z "$__CentosArch" ]; then
        echo $__BuildArch is unsupported architecture for Centos7.
        exit 1
    fi

    mkdir -p $__RootfsDir/var/lib/rpm $__RootfsDir/etc $__RootfsDir/dev $__RootfsDir/tmp $__RootfsDir/usr/bin

    mkdir -p $__RootfsDir/usr/bin

    $RPM --rebuilddb --root=$__RootfsDir

    mknod -m 666 $__RootfsDir/dev/null c 1 3
    mknod -m 666 $__RootfsDir/dev/random c 1 8
    mknod -m 666 $__RootfsDir/dev/urandom c 1 9

    cat $__CrossDir/$__BuildArch/centos7/packages | while read PACKAGE ; do
        if [ -z "$PACKAGE" ] || [[ $PACKAGE == \#* ]]; then
            continue
        fi
        echo installing $PACKAGE
        rpm -i --root=$__RootfsDir --dbpath=/var/lib/rpm/ --ignorearch --force --nodeps  http://mirror.centos.org/altarch/7/os/$__CentosArch/Packages/${PACKAGE}
    done

    cp  /usr/bin/qemu-$__QEMUArch-static $__RootfsDir/usr/bin
    # setup DNS. Some networks do no allow external lookup so start with host version.
    cp /etc/resolv.conf  $__RootfsDir/etc
    echo "nameserver 8.8.8.8" >> $__RootfsDir/etc/resolv.conf
    cp  /usr/bin/qemu-$__QEMUArch-static $__RootfsDir/usr/bin

    # Phase 1 is done. We should have enough to run commands from with roofs

    #cp $__CrossDir/$__BuildArch/CentOS7* $__RootfsDir/etc/yum.repos.d

    # update packages to current and fix any broken  dependencies from stage 1
    chroot $__RootfsDir yum update -y
    chroot $__RootfsDir yum -y reinstall rpm rpm-python yum yum-utils nss ca-certificates
    chroot $__RootfsDir yum --assumeyes install $__CentosPackages

    # Centos has symbolic links to /lib... and that does work only in chroot but not with sysfs.
    rm -f $__RootfsDir/usr/lib/gcc/$__CentosArch-redhat-linux/*/libgcc_s.so
    (cd $__RootfsDir/usr/lib/gcc/$__CentosArch-redhat-linux/4.8.5/ ; cp $__RootfsDir/lib64/libgcc_s-*.so.* libgcc_s.so)

    # build en_US.UTF-8 locale
    localedef -v -c -i en_US -f UTF-8 en_US.UTF-8
elif [[ -n $__LinuxCodeName ]]; then
    qemu-debootstrap --arch $__UbuntuArch $__LinuxCodeName $__RootfsDir $__UbuntuRepo
    cp $__CrossDir/$__BuildArch/sources.list.$__LinuxCodeName $__RootfsDir/etc/apt/sources.list
    chroot $__RootfsDir apt-get update
    chroot $__RootfsDir apt-get -f -y install
    chroot $__RootfsDir apt-get -y install $__UbuntuPackages
    chroot $__RootfsDir symlinks -cr /usr

    if [ $__SkipUnmount == 0 ]; then
        umount $__RootfsDir/*
    fi

    if [[ "$__BuildArch" == "arm" && "$__LinuxCodeName" == "trusty" ]]; then
        pushd $__RootfsDir
        patch -p1 < $__CrossDir/$__BuildArch/trusty.patch
        patch -p1 < $__CrossDir/$__BuildArch/trusty-lttng-2.4.patch
        popd
    fi
elif [ "$__Tizen" == "tizen" ]; then
    ROOTFS_DIR=$__RootfsDir $__CrossDir/$__BuildArch/tizen-build-rootfs.sh
else
    echo "Unsupported target platform."
    usage;
    exit 1
fi
