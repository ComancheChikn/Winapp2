﻿'    Copyright (C) 2018-2019 Robbie Ward
' 
'    This file is a part of Winapp2ool
' 
'    Winapp2ool is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    Winap2ool is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with Winapp2ool.  If not, see <http://www.gnu.org/licenses/>.
Option Strict On
''' <summary>
''' This module is echoes of JavaRA. It produces a winapp2.ini entry to remove irrelevant registry keys 
''' generated by the Java Runtime Environment installer. This module was made possible by JavaRA creator Fred de Vries
''' </summary>
Module JavaMaker
    ' Module parameters
    Dim download As Boolean = True
    Dim javaFile As New iniFile(Environment.CurrentDirectory, "java.ini")
    ''' <summary>
    ''' Prints the JavaMaker main menu
    ''' </summary>
    Public Sub printJMMenu()
        printMenuTop({"Creates a winapp2.ini entry that cleans up after old Java versions"})
        print(1, "Run (Default)", "Attempt to create an entry based on the current system", trailingBlank:=True)
        print(1, "Toggle Download", $"{enStr(download)} downloading of java.ini from GitHub")
        print(1, "File Chooser", "Select a new local file path for java.ini", Not download, trailingBlank:=True)
        print(0, $"Java definitions file: {replDir(javaFile.path)}")
    End Sub

    ''' <summary>
    ''' Handles the JavaMaker menu input
    ''' </summary>
    ''' <param name="input"></param>
    Public Sub handleJMInput(input As String)
        Select Case True
            Case input = "0"
                exitCode = True
            Case input = "1" Or input = ""
                makeSomeJava()
            Case input = "2" And Not download
                fileChooser(javaFile)
        End Select
    End Sub

    ''' <summary>
    ''' Generates the RegKeylist for the current system
    ''' </summary>
    ''' <param name="kls">A list of keylists containing the RegKeys that will be in the generated entry</param>
    ''' <returns></returns>
    Private Function mkEntry(kls As keyList()) As keyList
        Dim out As New keyList
        For Each lst In kls
            lst.removeLast()
            out.add(lst.keys)
        Next
        Return out
    End Function

    ''' <summary>
    ''' Creates RegKeys conditionally based on their presence on the current system
    ''' </summary>
    ''' <param name="reg">The Registry key to observe subkeys of</param>
    ''' <param name="searches">The strings to be searched for in the subkeys</param>
    ''' <returns></returns>
    Private Function getRegKeys(reg As Microsoft.Win32.RegistryKey, searches As List(Of String)) As List(Of iniKey)
        Dim out As New List(Of iniKey)
        Try
            Dim keys As List(Of String) = reg.GetSubKeyNames.ToList
            searches.ForEach(Sub(search) keys.ForEach(Sub(key) If key.Contains(search) Then out.Add(New iniKey($"RegKey1={reg.ToString}\{key}"))))
        Catch ex As Exception
            ' The only Exception we can expect here is that reg is not set to an instance of an object
            ' This occurs when the requested registry key does not exist on the current system
            ' We can just silently fail if that's the case 
        End Try
        Return out
    End Function

    ''' <summary>
    ''' Creates a winapp2.ini entry to clean up after the Java installation process
    ''' </summary>
    Private Sub makeSomeJava()
        ' Load the java.ini file
        If download Then javaFile = getRemoteIniFile(javaLink)
        javaFile.validate()
        ' Get JavaPlugin and JavaScript keys in HKCR\
        Dim JavaPluginKeys As New keyList(getRegKeys(Microsoft.Win32.Registry.ClassesRoot, {"JavaPlugin", "JavaScript"}.ToList))
        ' Get the CLSIDs present on the current system
        Dim clsids As New keyList("CLSID")
        Dim kll As New List(Of keyList) From {clsids, New keyList("Errors")}
        cwl("Running an intense registry query, this will take a few moments...")
        Dim jSect As iniSection = javaFile.sections.Item("Previous Java Installation Cleanup *")
        jSect.constKeyLists(kll)
        Dim IDS = clsids.toListOfStr(True)
        Dim typeLib As New keyList(getRegKeys(getCRKey("WOW6432Node\TypeLib\"), {"{5852F5E0-8BF4-11D4-A245-0080C6F74284}"}.ToList))
        Dim classRootIDs As New keyList(getRegKeys(getCRKey("WOW6432Node\CLSID\"), IDS))
        Dim localMachineClassesIDs As New keyList(getRegKeys(getLMKey("SOFTWARE\Classes\WOW6432Node\CLSID\"), IDS))
        Dim localMachineWOWIds As New keyList(getRegKeys(getLMKey("SOFTWARE\WOW6432Node\Classes\CLSID\"), IDS))
        Dim defClassesIDs As New keyList(getRegKeys(getUserKey(".DEFAULT\Software\Classes\CLSID\"), IDS))
        Dim s1518ClassesIDs As New keyList(getRegKeys(getUserKey("S-1-5-18\Software\Classes\CLSID\"), IDS))
        Dim localMachineJREs As New keyList(getRegKeys(getLMKey("Software\JavaSoft\Java Runtime Environment"), {"1"}.ToList))
        Dim lmJREminorIDs, cuJREminorIDs As New keyList
        localMachineJREs.keys.ForEach(Sub(key) lmJREminorIDs.add(key, key.toString.Replace("HKEY_LOCAL_MACHINE", "").Contains("_")))
        localMachineJREs.remove(lmJREminorIDs.keys)
        Dim currentUserJREs As New keyList(getRegKeys(getCUKey("Software\JavaSoft\Java Runtime Environment\"), {"1"}.ToList))
        currentUserJREs.keys.ForEach(Sub(key) cuJREminorIDs.add(key, key.toString.Replace("HKEY_CURRENT_USER", "").Contains("_")))
        currentUserJREs.remove(cuJREminorIDs.keys)
        ' Generate the list of RegKeys
        Dim regKeyList As keyList = mkEntry({classRootIDs, localMachineClassesIDs, localMachineWOWIds, defClassesIDs, s1518ClassesIDs,
                localMachineJREs, lmJREminorIDs, currentUserJREs, cuJREminorIDs, JavaPluginKeys})
        ' Renumber them 
        regKeyList.renumberKeys(replaceAndSort(regKeyList.toListOfStr(True), "-", "-"))
        ' Generate the new entry
        Dim entry As New List(Of String)
        entry.Add("[Java Installation Cleanup *]")
        entry.Add("Section=Experimental")
        entry.Add($"Detect={regKeyList.keys(0).value}")
        entry.Add("Default=False")
        entry.AddRange(regKeyList.toListOfStr)
        Dim out As New iniSection(entry)
        cwl(out.ToString)
        cwl() : cwl("Done")
        Console.ReadLine()
    End Sub
End Module
