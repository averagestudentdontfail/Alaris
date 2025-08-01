//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmHestonVarianceMesher : Fdm1dMesher {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FdmHestonVarianceMesher(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FdmHestonVarianceMesher_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmHestonVarianceMesher obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FdmHestonVarianceMesher(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FdmHestonVarianceMesher(uint size, HestonProcess process, double maturity, uint tAvgSteps, double epsilon) : this(NQuantLibcPINVOKE.new_FdmHestonVarianceMesher__SWIG_0(size, HestonProcess.getCPtr(process), maturity, tAvgSteps, epsilon), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmHestonVarianceMesher(uint size, HestonProcess process, double maturity, uint tAvgSteps) : this(NQuantLibcPINVOKE.new_FdmHestonVarianceMesher__SWIG_1(size, HestonProcess.getCPtr(process), maturity, tAvgSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmHestonVarianceMesher(uint size, HestonProcess process, double maturity) : this(NQuantLibcPINVOKE.new_FdmHestonVarianceMesher__SWIG_2(size, HestonProcess.getCPtr(process), maturity), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double volaEstimate() {
    double ret = NQuantLibcPINVOKE.FdmHestonVarianceMesher_volaEstimate(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
