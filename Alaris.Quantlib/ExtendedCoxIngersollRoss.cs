//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class ExtendedCoxIngersollRoss : CoxIngersollRoss {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal ExtendedCoxIngersollRoss(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.ExtendedCoxIngersollRoss_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(ExtendedCoxIngersollRoss obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_ExtendedCoxIngersollRoss(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public ExtendedCoxIngersollRoss(YieldTermStructureHandle termStructure, double theta, double k, double sigma, double x0) : this(NQuantLibcPINVOKE.new_ExtendedCoxIngersollRoss__SWIG_0(YieldTermStructureHandle.getCPtr(termStructure), theta, k, sigma, x0), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ExtendedCoxIngersollRoss(YieldTermStructureHandle termStructure, double theta, double k, double sigma) : this(NQuantLibcPINVOKE.new_ExtendedCoxIngersollRoss__SWIG_1(YieldTermStructureHandle.getCPtr(termStructure), theta, k, sigma), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ExtendedCoxIngersollRoss(YieldTermStructureHandle termStructure, double theta, double k) : this(NQuantLibcPINVOKE.new_ExtendedCoxIngersollRoss__SWIG_2(YieldTermStructureHandle.getCPtr(termStructure), theta, k), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ExtendedCoxIngersollRoss(YieldTermStructureHandle termStructure, double theta) : this(NQuantLibcPINVOKE.new_ExtendedCoxIngersollRoss__SWIG_3(YieldTermStructureHandle.getCPtr(termStructure), theta), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ExtendedCoxIngersollRoss(YieldTermStructureHandle termStructure) : this(NQuantLibcPINVOKE.new_ExtendedCoxIngersollRoss__SWIG_4(YieldTermStructureHandle.getCPtr(termStructure)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public YieldTermStructureHandle termStructure() {
    YieldTermStructureHandle ret = new YieldTermStructureHandle(NQuantLibcPINVOKE.ExtendedCoxIngersollRoss_termStructure(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
