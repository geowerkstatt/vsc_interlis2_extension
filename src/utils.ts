import * as os from "node:os";
import * as path from "node:path";

export const tempDir = path.join(os.tmpdir(), "InterlisLanguageSupport");
