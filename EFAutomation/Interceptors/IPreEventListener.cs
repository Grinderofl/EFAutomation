﻿using System.Data.Entity.Infrastructure;

<<<<<<< HEAD
namespace EFConventions.Interceptors
=======
namespace EFAutomation.Interceptors
>>>>>>> cf4659b760e07050a0426662dd0cba0acde539aa
{
    /// <summary>
    /// 
    /// </summary>
    public interface IPreEventListener
    {
        void OnEvent(DbEntityEntry entry);
    }
}