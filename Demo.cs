using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

class SafeObjectPoolDemo
{

    public void test()
    {

        var pool = new SafeObjectPool.ObjectPool<MemoryStream>(10, () => new MemoryStream(), obj =>
        {
            if (DateTime.Now.Subtract(obj.LastGetTime).TotalSeconds > 3)
            {
                var dt = Encoding.UTF8.GetBytes("我是中国人");
                obj.Value.Write(dt, 0, dt.Length);
            }
        });


        for (var a = 0; a < 100; a++)
        {
            new Thread(() =>
            {

                for (var b = 0; b < 1000; b++)
                {
                    var item = pool.Get();
                    pool.Return(item);
                }

                Console.WriteLine(pool.StatisticsFullily);


            }).Start();
        }
    }
}