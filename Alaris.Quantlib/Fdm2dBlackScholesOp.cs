//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class Fdm2dBlackScholesOp : FdmLinearOpComposite {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal Fdm2dBlackScholesOp(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.Fdm2dBlackScholesOp_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(Fdm2dBlackScholesOp obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_Fdm2dBlackScholesOp(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public Fdm2dBlackScholesOp(FdmMesher mesher, GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2, double correlation, double maturity, bool localVol, double illegalLocalVolOverwrite) : this(NQuantLibcPINVOKE.new_Fdm2dBlackScholesOp__SWIG_0(FdmMesher.getCPtr(mesher), GeneralizedBlackScholesProcess.getCPtr(p1), GeneralizedBlackScholesProcess.getCPtr(p2), correlation, maturity, localVol, illegalLocalVolOverwrite), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public Fdm2dBlackScholesOp(FdmMesher mesher, GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2, double correlation, double maturity, bool localVol) : this(NQuantLibcPINVOKE.new_Fdm2dBlackScholesOp__SWIG_1(FdmMesher.getCPtr(mesher), GeneralizedBlackScholesProcess.getCPtr(p1), GeneralizedBlackScholesProcess.getCPtr(p2), correlation, maturity, localVol), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public Fdm2dBlackScholesOp(FdmMesher mesher, GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2, double correlation, double maturity) : this(NQuantLibcPINVOKE.new_Fdm2dBlackScholesOp__SWIG_2(FdmMesher.getCPtr(mesher), GeneralizedBlackScholesProcess.getCPtr(p1), GeneralizedBlackScholesProcess.getCPtr(p2), correlation, maturity), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
