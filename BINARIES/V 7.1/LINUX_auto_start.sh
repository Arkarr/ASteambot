#!/bin/bash
cd $(dirname $0)

echo "*******************************"
echo "* ASTEAMBOT - LINUX INSTALLER *"
echo "*******************************"

#Get OS
if [ -f /etc/os-release ]; then
    # freedesktop.org and systemd
    . /etc/os-release
    OS=$NAME
    VER=$VERSION_ID
elif type lsb_release >/dev/null 2>&1; then
    # linuxbase.org
    OS=$(lsb_release -si)
    VER=$(lsb_release -sr)
elif [ -f /etc/lsb-release ]; then
    # For some versions of Debian/Ubuntu without lsb_release command
    . /etc/lsb-release
    OS=$DISTRIB_ID
    VER=$DISTRIB_RELEASE
elif [ -f /etc/debian_version ]; then
    # Older Debian/Ubuntu/etc.
    OS=Debian
    VER=$(cat /etc/debian_version)
elif [ -f /etc/SuSe-release ]; then
    # Older SuSE/etc.
    ...
elif [ -f /etc/redhat-release ]; then
    # Older Red Hat, CentOS, etc.
    ...
else
    # Fall back to uname, e.g. "Linux <version>", also works for BSD, etc.
    OS=$(uname -s)
    VER=$(uname -r)
fi

echo "*******************************"
echo "* OS FOUND : $OS - $VER *"
echo "*******************************"

echo "*******************************"
echo "*    ADDING REPO TO OS...     *"
echo "*******************************"

if [ "$OS" == "Ubuntu" ]; then
  if [ "$VER" == "16.04" ]; then
      sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
      echo "deb http://download.mono-project.com/repo/ubuntu xenial main" | sudo tee /etc/apt/sources.list.d/mono-official.list
  fi
  if [ "$VER" == "14.04" ]; then
      sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
      echo "deb http://download.mono-project.com/repo/ubuntu trusty main" | sudo tee /etc/apt/sources.list.d/mono-official.list
  fi
  if [ "$VER" == "12.04" ]; then
      sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
      echo "deb http://download.mono-project.com/repo/ubuntu precise main" | sudo tee /etc/apt/sources.list.d/mono-official.list
  fi

  sudo apt-get update

  echo "*******************************"
  echo "*     INSTALLING MONO...      *"
  echo "*******************************"

  sudo apt-get --force-yes --yes install mono-devel mono-complete referenceassemblies-pcl ca-certificates-mono
fi

if [ "$OS" == "Debian" ]; then
  if [ "$VER" == "9" ]; then
      sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
      echo "deb http://download.mono-project.com/repo/debian stretch main" | sudo tee /etc/apt/sources.list.d/mono-official.list
  fi
  if [ "$VER" == "8" ]; then
      sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
      echo "deb http://download.mono-project.com/repo/debian jessie main" | sudo tee /etc/apt/sources.list.d/mono-official.list
  fi
  if [ "$VER" == "7" ]; then
      sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
      echo "deb http://download.mono-project.com/repo/debian wheezy main" | sudo tee /etc/apt/sources.list.d/mono-official.list
  fi

  sudo apt-get update

  echo "*******************************"
  echo "*     INSTALLING MONO...      *"
  echo "*******************************"

  sudo apt-get --force-yes --yes install mono-devel mono-complete referenceassemblies-pcl ca-certificates-mono
fi

if [ "$OS" == "CentOS Linux" ]; then
  if [ "$VER" == "7" ]; then
      yum install yum-utils
      rpm --import "http://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
      yum-config-manager --add-repo http://download.mono-project.com/repo/centos7/
  fi
  if [ "$VER" == "6" ]; then
      yum install yum-utils
      rpm --import "http://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
      yum-config-manager --add-repo http://download.mono-project.com/repo/centos6/
  fi


  echo "*******************************"
  echo "*     INSTALLING MONO...      *"
  echo "*******************************"

  yum install mono-devel mono-complete referenceassemblies-pcl ca-certificates-mono
fi


echo "*******************************"
echo "* 	 STARTING ASTEAMBOT     *"
echo "*******************************"

mono "./ASteambot.exe"



