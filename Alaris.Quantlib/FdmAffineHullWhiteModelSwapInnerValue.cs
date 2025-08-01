//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmAffineHullWhiteModelSwapInnerValue : FdmInnerValueCalculator {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FdmAffineHullWhiteModelSwapInnerValue(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FdmAffineHullWhiteModelSwapInnerValue_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmAffineHullWhiteModelSwapInnerValue obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FdmAffineHullWhiteModelSwapInnerValue(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FdmAffineHullWhiteModelSwapInnerValue(HullWhite disModel, HullWhite fwdModel, FixedVsFloatingSwap swap, TimeToDateMap exerciseDates, FdmMesher mesher, uint direction) : this(NQuantLibcPINVOKE.new_FdmAffineHullWhiteModelSwapInnerValue(HullWhite.getCPtr(disModel), HullWhite.getCPtr(fwdModel), FixedVsFloatingSwap.getCPtr(swap), TimeToDateMap.getCPtr(exerciseDates), FdmMesher.getCPtr(mesher), direction), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
