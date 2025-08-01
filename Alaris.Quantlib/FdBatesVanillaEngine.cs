//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdBatesVanillaEngine : PricingEngine {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FdBatesVanillaEngine(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FdBatesVanillaEngine_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdBatesVanillaEngine obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FdBatesVanillaEngine(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FdBatesVanillaEngine(BatesModel model, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_0(BatesModel.getCPtr(model), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_1(BatesModel.getCPtr(model), tGrid, xGrid, vGrid, dampingSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, uint tGrid, uint xGrid, uint vGrid) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_2(BatesModel.getCPtr(model), tGrid, xGrid, vGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, uint tGrid, uint xGrid) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_3(BatesModel.getCPtr(model), tGrid, xGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, uint tGrid) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_4(BatesModel.getCPtr(model), tGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_5(BatesModel.getCPtr(model)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_6(BatesModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_7(BatesModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid, dampingSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_8(BatesModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, DividendSchedule dividends, uint tGrid, uint xGrid) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_9(BatesModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, DividendSchedule dividends, uint tGrid) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_10(BatesModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdBatesVanillaEngine(BatesModel model, DividendSchedule dividends) : this(NQuantLibcPINVOKE.new_FdBatesVanillaEngine__SWIG_11(BatesModel.getCPtr(model), DividendSchedule.getCPtr(dividends)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
