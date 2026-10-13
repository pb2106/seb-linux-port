#!/usr/bin/env python3
"""Converts a legacy (.NET Framework 4.8, old-style) csproj into an SDK-style net8.0 project.

Usage: migrate_csproj.py <project-dir> [<project-dir> ...]

The original project file is preserved as <name>.csproj.legacy so that the Windows build
can still be referenced and nothing is lost during the migration.
"""
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

NS = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}

# Packages that are referenced directly; everything else in packages.config is transitive.
DIRECT_PACKAGES = {
    "Newtonsoft.Json", "BouncyCastle.Cryptography", "ProDotNetZip",
    "Microsoft.ApplicationInsights", "Azure.Monitor.OpenTelemetry.Exporter",
    "OpenTelemetry.Extensions.Hosting", "Microsoft.Extensions.Logging", "Moq",
    "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Configuration",
    "Microsoft.Extensions.Configuration.EnvironmentVariables",
}
# Replace Windows-only packages by cross-platform ones.
SKIP_PACKAGES = {"Syroot.Windows.IO.KnownFolders", "FontAwesome.WPF", "OpenCover", "KGySoft.Drawing"}
PACKAGE_VERSION_OVERRIDES = {"BouncyCastle.Cryptography": "2.4.0"}
TEST_PACKAGES = [("MSTest", "4.3.2"), ("Microsoft.NET.Test.Sdk", "18.4.0")]

# Legacy framework (GAC) assembly references -> cross-platform NuGet packages.
REFERENCE_PACKAGES = {
    "System.ServiceModel": [("System.ServiceModel.Primitives", "8.1.2"), ("System.ServiceModel.NetNamedPipe", "8.1.2"),
                            ("System.ServiceModel.NetTcp", "8.1.2"), ("System.ServiceModel.Http", "8.1.2")],
    "System.Management": [("System.Management", "8.0.0")],
    "System.Drawing": [("System.Drawing.Common", "8.0.10")],
    "System.Configuration.Install": [],
    "System.ServiceProcess": [("System.ServiceProcess.ServiceController", "8.0.1")],
    "System.DirectoryServices": [("System.DirectoryServices", "8.0.0")],
}


def read_packages(directory):
    path = os.path.join(directory, "packages.config")
    result = []
    if os.path.exists(path):
        for node in ET.parse(path).getroot().iter("package"):
            pid, version = node.get("id"), node.get("version")
            if pid in DIRECT_PACKAGES and pid not in SKIP_PACKAGES:
                result.append((pid, PACKAGE_VERSION_OVERRIDES.get(pid, version)))
    return result


def unix(p):
    return p.replace("\\", "/")


def convert(directory):
    csproj = next(f for f in os.listdir(directory) if f.endswith(".csproj"))
    path = os.path.join(directory, csproj)
    if os.path.exists(path + ".legacy"):
        legacy = path + ".legacy"
    else:
        shutil.copy(path, path + ".legacy")
        legacy = path + ".legacy"

    text = open(legacy, encoding="utf-8-sig").read()
    root = ET.fromstring(text.encode("utf-8"))

    def first(tag):
        node = root.find(".//m:%s" % tag, NS)
        return node.text if node is not None else None

    output_type = first("OutputType") or "Library"
    root_ns = first("RootNamespace")
    assembly = first("AssemblyName")
    is_test = directory.rstrip("/").endswith("UnitTests")

    refs = [unix(n.get("Include")) for n in root.iterfind(".//m:ProjectReference", NS)]
    items = []
    for tag in ("EmbeddedResource", "None", "Content"):
        for n in root.iterfind(".//m:%s" % tag, NS):
            inc = unix(n.get("Include"))
            if inc.endswith((".config", ".settings", ".manifest", ".legacy")) or inc == "packages.config":
                continue
            items.append((tag, inc))

    lines = ['<Project Sdk="Microsoft.NET.Sdk">', "", "  <PropertyGroup>"]
    lines.append("    <TargetFramework>net8.0</TargetFramework>")
    if output_type.lower() != "library":
        lines.append("    <OutputType>%s</OutputType>" % ("Exe" if output_type != "WinExe" else "Exe"))
    if root_ns:
        lines.append("    <RootNamespace>%s</RootNamespace>" % root_ns)
    if assembly:
        lines.append("    <AssemblyName>%s</AssemblyName>" % assembly)
    lines += [
        "    <LangVersion>latest</LangVersion>",
        "    <Nullable>disable</Nullable>",
        "    <ImplicitUsings>disable</ImplicitUsings>",
        "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
        "    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>",
        "    <EnableDefaultNoneItems>false</EnableDefaultNoneItems>",
        "    <EnableUnsafeBinaryFormatterSerialization>true</EnableUnsafeBinaryFormatterSerialization>",
        "    <NoWarn>$(NoWarn);CS0618;CS0168;CS0219;CS0067;CS8981;SYSLIB0011;SYSLIB0014;SYSLIB0021;SYSLIB0022;SYSLIB0023;SYSLIB0041;SYSLIB0045;SYSLIB0051</NoWarn>",
    ]
    if is_test:
        lines += ["    <IsPackable>false</IsPackable>", "    <IsTestProject>true</IsTestProject>"]
    lines += ["  </PropertyGroup>", ""]

    pkgs = read_packages(directory)
    for n in root.iterfind(".//m:Reference", NS):
        pkgs += REFERENCE_PACKAGES.get(n.get("Include").split(",")[0], [])
    if "Monitoring" in directory or "SystemComponents" in directory:
        pkgs.append(("Microsoft.Win32.SystemEvents", "8.0.0"))
    if is_test:
        pkgs = [p for p in pkgs if p[0] != "Moq"] + [("Moq", "4.20.72")] + TEST_PACKAGES
    if pkgs:
        lines.append("  <ItemGroup>")
        for pid, version in dict(pkgs).items():
            lines.append('    <PackageReference Include="%s" Version="%s" />' % (pid, version))
        lines += ["  </ItemGroup>", ""]
    if refs:
        lines.append("  <ItemGroup>")
        for r in refs:
            lines.append('    <ProjectReference Include="%s" />' % r)
        lines += ["  </ItemGroup>", ""]
    if items:
        lines.append("  <ItemGroup>")
        for tag, inc in items:
            if tag == "EmbeddedResource":
                lines.append('    <EmbeddedResource Include="%s" />' % inc)
            elif tag == "Content":
                lines.append('    <Content Include="%s" CopyToOutputDirectory="PreserveNewest" />' % inc)
            else:
                lines.append('    <None Include="%s" />' % inc)
        lines += ["  </ItemGroup>", ""]
    lines.append("</Project>")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("converted", path)


if __name__ == "__main__":
    for d in sys.argv[1:]:
        convert(d)
