#include <windows.h>
#include <stdio.h>
#include <fstream>
#include <iostream>
#include <vector>
#include <TlHelp32.h>

extern "C" {
    void error(const char* info);
    void warn(const char* info);
}

BOOL open_process_in_job(char cmd[MAX_PATH], STARTUPINFOA* si, PROCESS_INFORMATION* pi, int options) {
    HANDLE job = CreateJobObjectA(NULL, NULL);
    if (!job) return FALSE;

    JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = {};
    info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
    SetInformationJobObject(job, JobObjectExtendedLimitInformation, &info, sizeof(info));

    if (!CreateProcessA(NULL, cmd, NULL, NULL, TRUE, options, NULL, NULL, si, pi)) {
        CloseHandle(job);
        std::string msg = "Failed to spawn process";
        error(msg.c_str());
        return FALSE;
    }

    if (!AssignProcessToJobObject(job, pi->hProcess)) {
        TerminateProcess(pi->hProcess, 0);
        CloseHandle(pi->hProcess);
        CloseHandle(pi->hThread);
        CloseHandle(job);
        std::string msg = "Failed to assign process a job object";
        error(msg.c_str());
        return FALSE;
    }

    return TRUE;
}

extern "C" {
    bool hollow_process(const unsigned char* payload, bool changelog) {
        STARTUPINFOA si = { sizeof(si) };
        PROCESS_INFORMATION pi = { 0 };
        char szPath[MAX_PATH];
        GetModuleFileNameA(NULL, szPath, MAX_PATH);

        std::string command = szPath;
        if (changelog) {
            command += " --changelog";
        }

        if (!open_process_in_job((char*)command.c_str(), &si, &pi, CREATE_SUSPENDED)) {
            return false;
        }

        PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)payload;
        PIMAGE_NT_HEADERS64 ntHeaders = (PIMAGE_NT_HEADERS64)(payload + dosHeader->e_lfanew);
        
        LPVOID remoteMem = VirtualAllocEx(pi.hProcess, (LPVOID)ntHeaders->OptionalHeader.ImageBase, ntHeaders->OptionalHeader.SizeOfImage, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

        if (!remoteMem) {
            TerminateProcess(pi.hProcess, 0);
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            std::string msg = "Failed to alloc memory";
            error(msg.c_str());
            
            return -1;
        }

        WriteProcessMemory(pi.hProcess, remoteMem, payload, ntHeaders->OptionalHeader.SizeOfHeaders, NULL);
        PIMAGE_SECTION_HEADER section = IMAGE_FIRST_SECTION(ntHeaders);
        for (int i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++) {
            LPVOID dest = (LPVOID)((size_t)remoteMem + section[i].VirtualAddress);
            LPVOID src = (LPVOID)((size_t)payload + section[i].PointerToRawData);
            WriteProcessMemory(pi.hProcess, dest, src, section[i].SizeOfRawData, NULL);
        }

        CONTEXT ctx = { 0 };
        ctx.ContextFlags = CONTEXT_ALL;
        GetThreadContext(pi.hThread, &ctx);

        WriteProcessMemory(pi.hProcess, (LPVOID)(ctx.Rdx + 0x10), &remoteMem, sizeof(LPVOID), NULL);

        ctx.Rip = (DWORD64)remoteMem + ntHeaders->OptionalHeader.AddressOfEntryPoint;
        ctx.Rcx = ctx.Rip; 

        SetThreadContext(pi.hThread, &ctx);

        if (ResumeThread(pi.hThread) == (DWORD)-1) {
            TerminateProcess(pi.hProcess, 0);
        }

        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);

        return true;
    }

    void run_in_job(const unsigned char* payload, size_t payload_size, bool changelog) {
        char tempPath[MAX_PATH];
        GetTempPathA(MAX_PATH, tempPath);

        std::string exePath = std::string(tempPath) + "Vice.Ui.exe";

        std::ofstream file(exePath, std::ios::binary);
        file.write(reinterpret_cast<const char*>(payload), payload_size);
        file.close();

        STARTUPINFOA si = { sizeof(si) };
        PROCESS_INFORMATION pi = { 0 };

        si.dwFlags |= STARTF_USESTDHANDLES;
        si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
        si.hStdError = GetStdHandle(STD_ERROR_HANDLE);
        si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

        if (changelog) {
            exePath += " --changelog";
        }

        if (!open_process_in_job((char*)exePath.c_str(), &si, &pi, 0)) {
            return;
        }

        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    }
}