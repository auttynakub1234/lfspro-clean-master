#!/bin/bash
set -e

echo "=== Compiling ProLight Clean Reborn ==="
cd "$(dirname "$0")"

mkdir -p bin

mcs -target:winexe \
    -platform:anycpu \
    -sdk:4.5 \
    -out:bin/ProLightClean.exe \
    src/SecureCrypto.cs \
    src/HardwareInfo.cs \
    src/LFSMemory.cs \
    src/MasterServerBridge.cs \
    src/ProfileManager.cs \
    src/LoginClient.cs \
    src/FormLauncher.cs \
    src/Program.cs \
    -r:System.Windows.Forms.dll \
    -r:System.Drawing.dll \
    -r:System.Management.dll \
    -r:System.Core.dll \
    -r:System.Net.Http.dll

cat << 'EOF' > bin/ProLightClean.exe.config
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
    <startup>
        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
    </startup>
</configuration>
EOF

echo "✓ Compilation Successful: bin/ProLightClean.exe"
ls -lh bin/ProLightClean.exe
