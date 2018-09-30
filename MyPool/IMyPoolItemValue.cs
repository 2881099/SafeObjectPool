using System;
using System.Collections.Generic;
using System.Text;

public interface ISafeObjectPool : IDisposable {

	void New();
}
