export type PuttyKeyErrorCode =
  | "sftpKeyInvalid"
  | "sftpKeyUnsupported"
  | "sftpKeyWrongPassphrase";

export class PuttyKeyError extends Error {
  constructor(readonly code: PuttyKeyErrorCode) {
    super(code);
  }
}

type Ppk = {
  version: number;
  algorithm: string;
  encryption: string;
  comment: string;
  publicData: Uint8Array;
  privateData: Uint8Array;
  mac: Uint8Array;
};

const text = new TextEncoder();

export function isEncryptedPuttyKey(value: string): boolean {
  return /^PuTTY-User-Key-File-\d+: .+\nEncryption: aes256-cbc(?:\n|$)/.test(
    value.trimStart().replace(/\r\n?/g, "\n"),
  );
}

export async function unlockPuttyKey(
  value: string,
  passphrase: string,
): Promise<string> {
  const ppk = parse(value);
  if (ppk.version !== 2 || ppk.encryption !== "aes256-cbc") {
    throw new PuttyKeyError("sftpKeyUnsupported");
  }
  if (!ppk.privateData.length || ppk.privateData.length % 16) {
    throw new PuttyKeyError("sftpKeyInvalid");
  }

  const password = text.encode(passphrase);
  const key = join(
    await sha1(join(uint32(0), password)),
    await sha1(join(uint32(1), password)),
  ).slice(0, 32);
  let privateData: Uint8Array | undefined;

  try {
    privateData = await decrypt(ppk.privateData, key);
    if (!equal(await privateMac(ppk, "aes256-cbc", privateData, password), ppk.mac)) {
      throw new PuttyKeyError("sftpKeyWrongPassphrase");
    }
    return render(
      ppk,
      privateData,
      await privateMac(ppk, "none", privateData, new Uint8Array()),
    );
  } catch (error: unknown) {
    if (error instanceof PuttyKeyError) throw error;
    throw new PuttyKeyError("sftpKeyInvalid");
  } finally {
    password.fill(0);
    key.fill(0);
    privateData?.fill(0);
  }
}

function parse(value: string): Ppk {
  try {
    const lines = value.trim().replace(/\r\n?/g, "\n").split("\n");
    const header = /^PuTTY-User-Key-File-(\d+): (.+)$/.exec(lines.shift() ?? "");
    if (!header) throw new Error();

    const field = (name: string): string => {
      const prefix = `${name}: `;
      const line = lines.shift();
      if (!line?.startsWith(prefix)) throw new Error();
      return line.slice(prefix.length);
    };
    const block = (name: string): Uint8Array => {
      const countText = field(`${name}-Lines`);
      if (!/^\d+$/.test(countText)) throw new Error();
      const count = Number(countText);
      const encoded = lines.splice(0, count);
      if (encoded.length !== count) throw new Error();
      return base64(encoded.join(""));
    };

    const encryption = field("Encryption");
    const comment = field("Comment");
    const publicData = block("Public");
    const privateData = block("Private");
    const mac = unhex(field("Private-MAC"));
    if (lines.some(Boolean)) throw new Error();
    return {
      version: Number(header[1]),
      algorithm: header[2] ?? "",
      encryption,
      comment,
      publicData,
      privateData,
      mac,
    };
  } catch {
    throw new PuttyKeyError("sftpKeyInvalid");
  }
}

async function decrypt(data: Uint8Array, key: Uint8Array): Promise<Uint8Array> {
  const cryptoKey = await crypto.subtle.importKey(
    "raw",
    buffer(key),
    "AES-CBC",
    false,
    ["encrypt", "decrypt"],
  );

  // Add one valid PKCS#7 block so WebCrypto preserves PuTTY's random padding.
  const padding = new Uint8Array(
    await crypto.subtle.encrypt(
      { name: "AES-CBC", iv: buffer(data.slice(-16)) },
      cryptoKey,
      new Uint8Array(),
    ),
  );
  const padded = join(data, padding);
  try {
    return new Uint8Array(
      await crypto.subtle.decrypt(
        { name: "AES-CBC", iv: new Uint8Array(16) },
        cryptoKey,
        buffer(padded),
      ),
    );
  } finally {
    padding.fill(0);
    padded.fill(0);
  }
}

async function privateMac(
  ppk: Ppk,
  encryption: string,
  privateData: Uint8Array,
  passphrase: Uint8Array,
): Promise<Uint8Array> {
  const key = await sha1(
    join(text.encode("putty-private-key-file-mac-key"), passphrase),
  );
  const hmac = await crypto.subtle.importKey(
    "raw",
    buffer(key),
    { name: "HMAC", hash: "SHA-1" },
    false,
    ["sign"],
  );
  const data = join(
    ssh(text.encode(ppk.algorithm)),
    ssh(text.encode(encryption)),
    ssh(text.encode(ppk.comment)),
    ssh(ppk.publicData),
    ssh(privateData),
  );
  try {
    return new Uint8Array(await crypto.subtle.sign("HMAC", hmac, buffer(data)));
  } finally {
    key.fill(0);
    data.fill(0);
  }
}

function render(ppk: Ppk, privateData: Uint8Array, mac: Uint8Array): string {
  const publicLines = base64Lines(ppk.publicData);
  const privateLines = base64Lines(privateData);
  return [
    `PuTTY-User-Key-File-2: ${ppk.algorithm}`,
    "Encryption: none",
    `Comment: ${ppk.comment}`,
    `Public-Lines: ${publicLines.length}`,
    ...publicLines,
    `Private-Lines: ${privateLines.length}`,
    ...privateLines,
    `Private-MAC: ${hex(mac)}`,
    "",
  ].join("\n");
}

const sha1 = async (value: Uint8Array) =>
  new Uint8Array(await crypto.subtle.digest("SHA-1", buffer(value)));
const ssh = (value: Uint8Array) => join(uint32(value.length), value);
const buffer = (value: Uint8Array): Uint8Array<ArrayBuffer> =>
  new Uint8Array(value);

function uint32(value: number): Uint8Array {
  const result = new Uint8Array(4);
  new DataView(result.buffer).setUint32(0, value);
  return result;
}

function join(...values: Uint8Array[]): Uint8Array {
  const result = new Uint8Array(values.reduce((sum, item) => sum + item.length, 0));
  let offset = 0;
  for (const value of values) {
    result.set(value, offset);
    offset += value.length;
  }
  return result;
}

function equal(left: Uint8Array, right: Uint8Array): boolean {
  if (left.length !== right.length) return false;
  return left.reduce(
    (difference, byte, index) => difference | (byte ^ (right[index] ?? 0)),
    0,
  ) === 0;
}

function base64(value: string): Uint8Array {
  return Uint8Array.from(atob(value), (character) => character.charCodeAt(0));
}

function base64Lines(value: Uint8Array): string[] {
  let binary = "";
  for (const byte of value) binary += String.fromCharCode(byte);
  return btoa(binary).match(/.{1,64}/g) ?? [];
}

function unhex(value: string): Uint8Array {
  if (!/^[0-9a-f]{40}$/i.test(value)) throw new Error();
  return Uint8Array.from(value.match(/../g) ?? [], (byte) => parseInt(byte, 16));
}

function hex(value: Uint8Array): string {
  return Array.from(value, (byte) => byte.toString(16).padStart(2, "0")).join("");
}
