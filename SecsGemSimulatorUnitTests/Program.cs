//Unit test  for solution SecsGemSimulator
// This file is part of the SecsGemSimulatorUnitTests project
// Copyright DCSS LLC. All rights reserved.
// David Strickland 2025

using System.Windows.Forms;
using System;
namespace SecsGemSimulatorUnitTests
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}