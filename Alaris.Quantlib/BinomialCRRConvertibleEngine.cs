//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class BinomialCRRConvertibleEngine : PricingEngine {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal BinomialCRRConvertibleEngine(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.BinomialCRRConvertibleEngine_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(BinomialCRRConvertibleEngine obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_BinomialCRRConvertibleEngine(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public BinomialCRRConvertibleEngine(GeneralizedBlackScholesProcess arg0, uint steps, QuoteHandle creditSpread, DividendSchedule dividends) : this(NQuantLibcPINVOKE.new_BinomialCRRConvertibleEngine__SWIG_0(GeneralizedBlackScholesProcess.getCPtr(arg0), steps, QuoteHandle.getCPtr(creditSpread), DividendSchedule.getCPtr(dividends)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public BinomialCRRConvertibleEngine(GeneralizedBlackScholesProcess arg0, uint steps, QuoteHandle creditSpread) : this(NQuantLibcPINVOKE.new_BinomialCRRConvertibleEngine__SWIG_1(GeneralizedBlackScholesProcess.getCPtr(arg0), steps, QuoteHandle.getCPtr(creditSpread)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
