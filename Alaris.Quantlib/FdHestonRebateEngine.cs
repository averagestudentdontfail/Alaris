//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdHestonRebateEngine : PricingEngine {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FdHestonRebateEngine(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FdHestonRebateEngine_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdHestonRebateEngine obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FdHestonRebateEngine(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc, LocalVolTermStructure leverageFct, double mixingFactor) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_0(HestonModel.getCPtr(model), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc), LocalVolTermStructure.getCPtr(leverageFct), mixingFactor), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc, LocalVolTermStructure leverageFct) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_1(HestonModel.getCPtr(model), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc), LocalVolTermStructure.getCPtr(leverageFct)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_2(HestonModel.getCPtr(model), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_3(HestonModel.getCPtr(model), tGrid, xGrid, vGrid, dampingSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid, uint xGrid, uint vGrid) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_4(HestonModel.getCPtr(model), tGrid, xGrid, vGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid, uint xGrid) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_5(HestonModel.getCPtr(model), tGrid, xGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, uint tGrid) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_6(HestonModel.getCPtr(model), tGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_7(HestonModel.getCPtr(model)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc, LocalVolTermStructure leverageFct, double mixingFactor) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_8(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc), LocalVolTermStructure.getCPtr(leverageFct), mixingFactor), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc, LocalVolTermStructure leverageFct) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_9(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc), LocalVolTermStructure.getCPtr(leverageFct)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps, FdmSchemeDesc schemeDesc) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_10(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid, dampingSteps, FdmSchemeDesc.getCPtr(schemeDesc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid, uint dampingSteps) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_11(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid, dampingSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid, uint xGrid, uint vGrid) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_12(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid, vGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid, uint xGrid) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_13(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid, xGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends, uint tGrid) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_14(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends), tGrid), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdHestonRebateEngine(HestonModel model, DividendSchedule dividends) : this(NQuantLibcPINVOKE.new_FdHestonRebateEngine__SWIG_15(HestonModel.getCPtr(model), DividendSchedule.getCPtr(dividends)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
