//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BinomialTrigeorgisBarrierEngine : PricingEngine {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal BinomialTrigeorgisBarrierEngine(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.BinomialTrigeorgisBarrierEngine_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BinomialTrigeorgisBarrierEngine obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_BinomialTrigeorgisBarrierEngine(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public BinomialTrigeorgisBarrierEngine(GeneralizedBlackScholesProcess arg0, uint steps, uint max_steps) : this(NQuantLibcPINVOKE.new_BinomialTrigeorgisBarrierEngine__SWIG_0(GeneralizedBlackScholesProcess.getCPtr(arg0), steps, max_steps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BinomialTrigeorgisBarrierEngine(GeneralizedBlackScholesProcess arg0, uint steps) : this(NQuantLibcPINVOKE.new_BinomialTrigeorgisBarrierEngine__SWIG_1(GeneralizedBlackScholesProcess.getCPtr(arg0), steps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
